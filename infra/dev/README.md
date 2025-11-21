# Ambiente Dev - Containers Locais

## Requisitos
- Docker Desktop ou engine compatível com suporte a Docker Compose V2.

## Subindo serviços

### 1. Iniciar containers
```powershell
docker compose -f infra/dev/docker-compose.yml up -d
```

Os containers serão criados e iniciados em segundo plano:
- **desafio-rabbitmq**: RabbitMQ 3.13 com Management Plugin
- **desafio-sqlserver**: SQL Server 2022 Developer Edition

### 2. Verificar status
```powershell
docker ps --filter "name=desafio-"
```

### 3. Aguardar inicialização
SQL Server leva ~30-45s para aceitar conexões. Teste conectividade:
```powershell
docker exec desafio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C -Q "SELECT @@VERSION"
```

RabbitMQ leva ~10-15s. Acesse o Management Console:
- URL: http://localhost:15672
- Usuário: `admin` / Senha: `admin123`

### 4. Executar migrations (Inventory Service)
```powershell
# Navegar para o projeto da API
cd src/Services/InventoryService/Inventory.Api

# Aplicar migrations ao banco
dotnet ef database update

# Retornar ao root
cd ../../../..
```

## Serviços disponíveis

### RabbitMQ
- **AMQP Protocol**: `amqp://admin:admin123@localhost:5672`
- **Management Console**: http://localhost:15672
- **Credenciais**: admin / admin123
- **Exchange para eventos**: `sales.events` (configurado pelo Sales Service)

### SQL Server
- **Endpoint**: `localhost,1433`
- **Usuário**: `sa`
- **Senha**: `YourStrong@Passw0rd`
- **Databases criados pelas migrations**:
  - `InventoryDb` (Inventory Service)
  - `SalesDb` (Sales Service - futuro)

## Fluxo completo de execução local

```powershell
# 1. Subir dependências (RabbitMQ + SQL Server)
docker compose -f infra/dev/docker-compose.yml up -d

# 2. Aguardar inicialização (~45s)
Start-Sleep -Seconds 45

# 3. Aplicar migrations do Inventory Service
cd src/Services/InventoryService/Inventory.Api
dotnet ef database update
cd ../../../..

# 4. Iniciar Gateway (Terminal 1)
dotnet run --project src/ApiGateway/ApiGateway.csproj

# 5. Iniciar Auth Service (Terminal 2)
dotnet run --project src/AuthService/Auth.Api/Auth.Api.csproj

# 6. Iniciar Inventory Service (Terminal 3)
dotnet run --project src/Services/InventoryService/Inventory.Api/Inventory.Api.csproj

# 7. Iniciar Sales Service (Terminal 4 - futuro)
dotnet run --project src/Services/SalesService/Sales.Api/Sales.Api.csproj
```

### Endpoints de health check
- Gateway: http://localhost:5000/health
- Auth Service: http://localhost:5001/health
- Inventory Service: http://localhost:5002/api/v1/inventory/health

### Testando o fluxo completo

1. Obter token JWT:
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/auth/token" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"admin123"}'
$token = $response.access_token
```

2. Criar produto via Gateway:
```powershell
$headers = @{ Authorization = "Bearer $token" }
$body = @{
    sku = "PROD-001"
    name = "Produto Teste"
    description = "Descrição do produto teste"
    price = 99.90
    quantityAvailable = 100
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/inventory/products" -Method Post -Headers $headers -ContentType "application/json" -Body $body
```

3. Listar produtos:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/inventory/products?page=1&pageSize=10" -Headers $headers
```

## Encerrando

```powershell
# Parar containers mantendo volumes
docker compose -f infra/dev/docker-compose.yml down

# Parar containers e remover volumes (reset completo)
docker compose -f infra/dev/docker-compose.yml down -v
```

## Logs e troubleshooting

### Visualizar logs dos containers
```powershell
# SQL Server
docker logs desafio-sqlserver --tail 50

# RabbitMQ
docker logs desafio-rabbitmq --tail 50
```

### Conectar no SQL Server via container
```powershell
docker exec -it desafio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -C
```

### Acessar RabbitMQ Management
Navegue para http://localhost:15672 e use `admin/admin123` para visualizar:
- Exchanges configurados
- Queues criadas pelos consumers
- Mensagens em trânsito
- Conexões ativas

## Observações

- **Senhas**: Altere credenciais antes de provisionar ambientes compartilhados (staging/produção).
- **Migrations**: Sempre execute `dotnet ef database update` após subir o SQL Server pela primeira vez.
- **Volumes**: Dados persistem em volumes Docker (`sqlserver_data`, `rabbitmq_data`). Use `down -v` para reset completo.
- **Networking**: Containers compartilham rede `dev_default` criada automaticamente pelo Docker Compose.
- **Ports**: Certifique-se de que portas 1433, 5672 e 15672 não estejam em uso por outros processos.
