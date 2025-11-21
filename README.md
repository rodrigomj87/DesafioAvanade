# Desafio Avanade - Plataforma E-commerce

Base inicial dos microserviços descritos em `Docs/DocumentoBase.md`.

## Estrutura
```
src/
  ApiGateway/                -> Gateway com YARP
  AuthService/Auth.Api/      -> Serviço de autenticação stub
  Services/
    InventoryService/
      Inventory.Api/
      Inventory.Application/
      Inventory.Domain/
      Inventory.Infrastructure/
    SalesService/
      Sales.Api/
      Sales.Application/
      Sales.Domain/
      Sales.Infrastructure/
.github/workflows/           -> Workflows GitHub Actions
infra/dev/                   -> Docker Compose (RabbitMQ + SQL Server)
Docs/                        -> Documento base, ADRs, backlog, specs
```

## Rodando localmente
1. Suba dependências (SQL Server + RabbitMQ):
  ```powershell
  docker compose -f infra/dev/docker-compose.yml up -d
  ```
2. Crie/atualize o banco Inventory (contexto `InventoryDbContext`):
  ```powershell
  dotnet ef database update --project src/Services/InventoryService/Inventory.Infrastructure/Inventory.Infrastructure.csproj --startup-project src/Services/InventoryService/Inventory.Api/Inventory.Api.csproj
  ```
3. Execute serviços conforme necessário (cada um em terminal próprio):
  ```powershell
  dotnet run --project src/AuthService/Auth.Api/Auth.Api.csproj
  dotnet run --project src/Services/InventoryService/Inventory.Api/Inventory.Api.csproj
  dotnet run --project src/Services/SalesService/Sales.Api/Sales.Api.csproj
  dotnet run --project src/ApiGateway/ApiGateway.csproj --urls http://localhost:5000
  ```
  O gateway consome as URLs internas definidas em `appsettings.Development.json` (`Services:Inventory`, `Services:Sales`). Ajuste-as se mudar as portas dos microserviços.
4. Health-checks: `http://localhost:5000/health` (gateway), `/api/v1/inventory/health`, `/api/v1/sales/health`, `/health` (Auth). Swagger disponível em `/swagger` nos serviços.

  ## JWT / JWKS (Autenticação RS256)
  - O Auth Service expõe `/.well-known/jwks.json` e assina tokens RS256 via `POST /auth/token`.
  - O gateway usa **dynamic JWKS discovery** via `JwksBackgroundService`:
    - Carrega JWKS do Auth Service no startup de forma assíncrona
    - Retry logic com 10 tentativas e delay incremental (2-11 segundos)
    - Armazena chaves em `JwksHolder` thread-safe para validação JWT
  - Configuração no `appsettings.Development.json`:
    ```json
    "Jwt": {
      "Issuer": "https://auth.local",
      "Audience": "desafio-avanade",
      "JwksMode": "Remote",
      "JwksEndpoint": "http://localhost:5112/.well-known/jwks.json"
    }
    ```
  - Para validar o fluxo end-to-end, use o script automatizado:
    ```powershell
    # Executa teste completo: Auth + Inventory + Gateway + validações
    .\Docs\demo\e2e-test.ps1
    ```
    O script valida:
    - ✅ Containers Docker (SQL Server + RabbitMQ)
    - ✅ Health checks de todos os serviços
    - ✅ Obtenção de token JWT do Auth Service
    - ✅ Criação de produto via Gateway com autenticação
    - ✅ Paginação via Gateway
    - ✅ Logs estruturados JSON + OpenTelemetry traces

  Conforme ADR-003, o Gateway valida JWT RS256 usando JWKS publicado dinamicamente pelo Auth Service.

### Portas dos Serviços
- **Auth Service**: `http://localhost:5112`
- **Inventory Service**: `http://localhost:5148`
- **Gateway**: `http://localhost:5152`
- **SQL Server**: `localhost:1433` (sa/YourStrong@Passw0rd)
- **RabbitMQ**: `localhost:5672` (guest/guest) + Management UI `localhost:15672`

### Validação Manual Rápida
Para testes manuais individuais:

```powershell
# 1. Obter token do Auth Service
$tokenResponse = Invoke-RestMethod -Method Post -Uri "http://localhost:5112/auth/token" `
  -ContentType "application/json" `
  -Body '{"username":"admin","password":"admin"}'

# 2. Criar produto via Gateway (autenticado)
$headers = @{ Authorization = "Bearer $($tokenResponse.token)" }
$product = @{
  sku = "TEST-001"
  name = "Produto Teste"
  description = "Teste manual"
  price = 99.90
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://localhost:5152/inventory/products" `
  -Headers $headers -ContentType "application/json" -Body $product

# 3. Listar produtos via Gateway
Invoke-RestMethod -Method Get -Uri "http://localhost:5152/inventory/products?page=1&pageSize=10" `
  -Headers $headers
```

## Observabilidade, Health & Rate Limiting
- Endpoints públicos: `GET /health` e `GET /healthz` continuam anônimos e respondem `200`, úteis para probes Kubernetes/Azure (`/healthz` é exposto diretamente pelo `UseGatewayPipeline()` em `Program.cs`).
- O middleware de correlação garante que toda requisição receba/propague `X-Correlation-ID`, presente nos logs e encaminhado aos serviços behind o gateway.
- Toda a configuração do gateway foi encapsulada em extensions (`ConfigureGatewayLogging`, `AddGatewayObservability`, `AddGatewaySecurity`, `UseGatewayPipeline`), mantendo o `Program.cs` enxuto seguindo KISS e garantindo que Serilog JSON, OpenTelemetry (AspNetCore/HttpClient/Runtime) e rate limiting estejam sempre habilitados em conjunto.
- Logs utilizam Serilog em JSON estruturado (`UseSerilog` + `UseSerilogRequestLogging`), facilitando coleta em ferramentas centralizadas.
- OpenTelemetry já está habilitado com exporters de console para traces e métricas (AspNetCore + HttpClient + Runtime). Para desativar/alterar exporters use variáveis `OTEL_*` ou edite as extensions mencionadas acima.
- Rate limiting: por padrão são permitidas 30 requisições a cada 10 segundos por cliente (IP). Configure via `appsettings*` na seção `RateLimiting` ou via env vars (`RateLimiting__PermitLimit`, `RateLimiting__WindowSeconds`, `RateLimiting__QueueLimit`). Apenas as rotas proxy (`/inventory`, `/sales`) estão sujeitas ao limitador.

## Workflows
- `dotnet-ci.yml`: restaura, compila, executa testes e valida formatação (`dotnet format --verify-no-changes`) conforme ADR-005.
- `gateway-ci.yml`: build/test do `src/ApiGateway/ApiGateway.csproj` sempre que houver mudanças no gateway ou documentação relacionada.

## Próximos passos
- Implementar integrações reais com RabbitMQ e SQL Server seguindo ADR-001 e ADR-002.
- Expandir testes (xUnit) em `tests/` e configurar publicação para GHCR quando os serviços estiverem prontos.
