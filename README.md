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
2. Execute serviços conforme necessário (cada um em terminal próprio):
  ```powershell
  dotnet run --project src/AuthService/Auth.Api/Auth.Api.csproj
  dotnet run --project src/Services/InventoryService/Inventory.Api/Inventory.Api.csproj
  dotnet run --project src/Services/SalesService/Sales.Api/Sales.Api.csproj
  dotnet run --project src/ApiGateway/ApiGateway.csproj --urls http://localhost:5000
  ```
  O gateway consome as URLs internas definidas em `appsettings.Development.json` (`Services:Inventory`, `Services:Sales`). Ajuste-as se mudar as portas dos microserviços.
3. Health-checks: `http://localhost:5000/health` (gateway), `/api/v1/inventory/health`, `/api/v1/sales/health`, `/health` (Auth). Swagger disponível em `/swagger` nos serviços.

  ## JWT / JWKS
  - O stub de autenticação agora expõe `/.well-known/jwks.json` e assina tokens RS256 via `POST /api/v1/auth/token`.
  - O gateway aceita duas formas de carregar a chave pública:
    - `Jwt:JwksMode = Inline` (default): usa o bloco `Jwt:Jwks` do `appsettings*`.
    - `Jwt:JwksMode = Remote`: baixa o JWKS de `Jwt:JwksEndpoint` (ex.: `http://localhost:5010/.well-known/jwks.json`).
  - Para validar o fluxo end-to-end com o Auth stub:
    1. Suba o Auth service: 
      ```powershell
      dotnet run --project src/AuthService/Auth.Api/Auth.Api.csproj --urls http://localhost:5010
      ```
    2. Execute o gateway com overrides apontando para o JWKS remoto:
      ```powershell
      $env:Jwt__JwksMode = "Remote"
      $env:Jwt__JwksEndpoint = "http://localhost:5010/.well-known/jwks.json"
      dotnet run --project src/ApiGateway/ApiGateway.csproj --urls http://localhost:5000
      ```
    3. Gere um token e chame o gateway:
      ```powershell
      $tokenResponse = Invoke-RestMethod -Method Post -Uri "http://localhost:5010/api/v1/auth/token" -ContentType "application/json" -Body '{"username":"tester","password":"pwd"}'
      Invoke-RestMethod -Method Get -Uri "http://localhost:5000/inventory/api/v1/inventory/health" -Headers @{ Authorization = "Bearer $($tokenResponse.accessToken)" }
      ```
      4. Remova as variáveis de ambiente quando finalizar:
        ```powershell
        Remove-Item Env:Jwt__JwksMode
        Remove-Item Env:Jwt__JwksEndpoint
        ```

  Esses passos garantem que o gateway valide o JWT exatamente com o JWKS publicado pelo Auth Service, reproduzindo o cenário descrito no ADR-003.

### Script PowerShell rápido
Para agilizar a validação manual pode-se usar `Start-Job` para subir cada serviço e repetir os mesmos requests:

```powershell
$global:AuthJob = Start-Job -ScriptBlock {
    Set-Location 'd:/dev/DesafioAvanade'
    dotnet run --project src/AuthService/Auth.Api/Auth.Api.csproj --urls http://localhost:5010
}; Start-Sleep -Seconds 5

$global:InventoryJob = Start-Job -ScriptBlock {
    Set-Location 'd:/dev/DesafioAvanade'
    dotnet run --project src/Services/InventoryService/Inventory.Api/Inventory.Api.csproj --urls http://localhost:5101
}; Start-Sleep -Seconds 5

$global:GatewayJob = Start-Job -ScriptBlock {
    Set-Location 'd:/dev/DesafioAvanade'
    $env:Jwt__JwksMode = 'Remote'
    $env:Jwt__JwksEndpoint = 'http://localhost:5010/.well-known/jwks.json'
    dotnet run --project src/ApiGateway/ApiGateway.csproj --urls http://localhost:5000
}; Start-Sleep -Seconds 5

$body = @{ username = 'tester'; password = 'pwd'; roles = @('inventory.read') } | ConvertTo-Json
$global:tokenResponse = Invoke-RestMethod -Method Post -Uri 'http://localhost:5010/api/v1/auth/token' -ContentType 'application/json' -Body $body

$headers = @{ Authorization = "Bearer $($global:tokenResponse.accessToken)" }
Invoke-RestMethod -Method Get -Uri 'http://localhost:5000/inventory/api/v1/inventory/health' -Headers $headers

Stop-Job -Id $GatewayJob.Id,$InventoryJob.Id,$AuthJob.Id
Receive-Job -Id $GatewayJob.Id -Keep
Receive-Job -Id $InventoryJob.Id -Keep
Receive-Job -Id $AuthJob.Id -Keep
Remove-Job -Id $GatewayJob.Id,$InventoryJob.Id,$AuthJob.Id
```

O bloco final mostra os logs antes de remover os jobs para liberar os binários.

## Workflows
- `dotnet-ci.yml`: restaura, compila, executa testes e valida formatação (`dotnet format --verify-no-changes`) conforme ADR-005.
- `gateway-ci.yml`: build/test do `src/ApiGateway/ApiGateway.csproj` sempre que houver mudanças no gateway ou documentação relacionada.

## Próximos passos
- Implementar integrações reais com RabbitMQ e SQL Server seguindo ADR-001 e ADR-002.
- Expandir testes (xUnit) em `tests/` e configurar publicação para GHCR quando os serviços estiverem prontos.
