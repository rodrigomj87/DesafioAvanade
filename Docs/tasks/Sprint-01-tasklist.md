# Sprint 01 - Task List

## Objetivo
Estabelecer a base da plataforma de e-commerce com microsserviços, API Gateway, autenticação e observabilidade.

---

## Task 1: Estrutura Base do Projeto ✅
**Status**: Concluída

### Descrição
Criar a estrutura inicial do projeto com solution .NET e estrutura de pastas para microsserviços.

### Entregáveis
- [x] Arquivo DesafioAvanade.sln
- [x] Estrutura de diretórios (src/, tests/, infra/, Docs/)
- [x] .gitignore configurado
- [x] README.md inicial

---

## Task 2: Infraestrutura de Desenvolvimento ✅
**Status**: Concluída

### Descrição
Configurar ambiente de desenvolvimento local com Docker Compose para dependências.

### Entregáveis
- [x] Docker Compose com SQL Server
- [x] Docker Compose com RabbitMQ
- [x] Documentação de setup em infra/dev/README.md
- [x] Scripts de inicialização

---

## Task 3: Microsserviços Base ✅
**Status**: Concluída

### Descrição
Implementar estrutura básica dos microsserviços com health checks.

### Entregáveis
- [x] AuthService (Auth.Api) - Serviço stub de autenticação
  - [x] Geração de tokens JWT RS256
  - [x] Endpoint JWKS (/.well-known/jwks.json)
  - [x] Health check
- [x] InventoryService - Estrutura em camadas
  - [x] Inventory.Api
  - [x] Inventory.Application
  - [x] Inventory.Domain
  - [x] Inventory.Infrastructure
  - [x] Health check
- [x] SalesService - Estrutura em camadas
  - [x] Sales.Api
  - [x] Sales.Application
  - [x] Sales.Domain
  - [x] Sales.Infrastructure
  - [x] Health check

---

## Task 4: API Gateway com Roteamento Seguro e OpenTelemetry ✅
**Status**: Concluída

### Descrição
Implementar API Gateway usando YARP com autenticação JWT, rate limiting e observabilidade completa via OpenTelemetry.

### Entregáveis
- [x] **Roteamento Reverso**
  - [x] Configuração YARP para Inventory Service
  - [x] Configuração YARP para Sales Service
  - [x] Provider dinâmico de configuração (GatewayProxyConfigProvider)

- [x] **Autenticação JWT**
  - [x] Middleware de autenticação JWT
  - [x] Suporte a JWKS inline (appsettings)
  - [x] Suporte a JWKS remoto (Auth Service)
  - [x] Validação de issuer, audience e lifetime
  - [x] Configuração via JwtConfigurationBuilder

- [x] **Rate Limiting**
  - [x] Rate limiting por IP
  - [x] Configuração via appsettings (RateLimitingSettings)
  - [x] Política "gateway-default"
  - [x] Exclusão de health checks do rate limiting
  - [x] Resposta 429 Too Many Requests

- [x] **Observabilidade (OpenTelemetry)**
  - [x] Instrumentação AspNetCore
  - [x] Instrumentação HttpClient
  - [x] Instrumentação Runtime
  - [x] Traces com exportador console
  - [x] Métricas com exportador console
  - [x] Resource com nome do serviço

- [x] **Correlation IDs**
  - [x] Middleware de correlação
  - [x] Propagação para serviços downstream
  - [x] Inclusão nos logs
  - [x] Header X-Correlation-ID

- [x] **Logging Estruturado**
  - [x] Configuração Serilog
  - [x] Formato JSON
  - [x] Request logging
  - [x] Enrichment com Correlation ID

- [x] **Health Checks**
  - [x] Endpoint /health (anônimo, sem rate limit)
  - [x] Endpoint /healthz (anônimo, sem rate limit)

- [x] **Extensions e Organização**
  - [x] GatewayServiceCollectionExtensions
  - [x] WebApplicationBuilderExtensions
  - [x] WebApplicationExtensions
  - [x] Program.cs limpo e enxuto (KISS)

- [x] **Testes**
  - [x] AuthenticationTests
    - [x] Token ausente retorna 401
    - [x] Token válido permite acesso
    - [x] Token expirado retorna 401
  - [x] HealthAndRateLimitTests
    - [x] Health checks retornam 200 sem autenticação
    - [x] Rate limit excedido retorna 429
  - [x] Factories e stubs de teste
  - [x] Helper TestJwtToken

- [x] **Documentação**
  - [x] README.md atualizado com instruções
  - [x] Exemplos de uso com PowerShell
  - [x] Configuração de variáveis de ambiente

### Observações Técnicas

#### Autenticação
O gateway suporta dois modos de operação para validação JWT:
1. **Modo Inline** (padrão): A chave pública RSA é configurada diretamente no appsettings.json
2. **Modo Remoto**: A chave é obtida do endpoint JWKS do Auth Service

Para usar o modo remoto (desenvolvimento local):
```powershell
$env:Jwt__JwksMode = "Remote"
$env:Jwt__JwksEndpoint = "http://localhost:5010/.well-known/jwks.json"
```

> **Nota**: Em ambientes de produção, substitua `localhost:5010` pela URL real do Auth Service (ex: `https://auth.example.com/.well-known/jwks.json`).

#### Rate Limiting
Configuração padrão:
- **PermitLimit**: 30 requisições
- **WindowSeconds**: 10 segundos
- **QueueLimit**: 0 (sem fila)

#### OpenTelemetry
Atualmente configurado com exportadores console para desenvolvimento. Em produção, deve ser configurado com exportadores apropriados (OTLP, Jaeger, etc.).

### Arquivos Criados/Modificados

#### Novos Arquivos
- src/ApiGateway/Program.cs
- src/ApiGateway/ApiGateway.csproj
- src/ApiGateway/appsettings.json
- src/ApiGateway/appsettings.Development.json
- src/ApiGateway/GatewayProxyConfigProvider.cs
- src/ApiGateway/Extensions/GatewayServiceCollectionExtensions.cs
- src/ApiGateway/Extensions/WebApplicationBuilderExtensions.cs
- src/ApiGateway/Extensions/WebApplicationExtensions.cs
- src/ApiGateway/Configurations/JwtConfigurationBuilder.cs
- src/ApiGateway/Configurations/RateLimitingSettings.cs
- tests/ApiGateway.Tests/*.cs

#### Arquivos Modificados
- README.md (seções de observabilidade, JWT e rate limiting)

### Dependências
- Yarp.ReverseProxy
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.IdentityModel.Tokens
- OpenTelemetry.Exporter.Console
- OpenTelemetry.Extensions.Hosting
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Instrumentation.Runtime
- Serilog.AspNetCore
- Serilog.Sinks.Console
- FluentAssertions (testes)
- xUnit (testes)

---

## Task 5: CI/CD com GitHub Actions ✅
**Status**: Concluída

### Descrição
Configurar workflows de CI/CD para build, test e validação de código.

### Entregáveis
- [x] Workflow dotnet-ci.yml (build + test + format)
- [x] Workflow gateway-ci.yml (build/test específico do gateway)
- [x] Validação de formatação (dotnet format --verify-no-changes)

---

## Task 6: Integração com SQL Server 📋
**Status**: Planejada

### Descrição
Implementar repositórios reais com Entity Framework Core e SQL Server.

### Entregáveis Planejados
- [ ] DbContext para Inventory
- [ ] DbContext para Sales
- [ ] Migrations
- [ ] Repositórios EF Core
- [ ] Connection strings configuráveis
- [ ] Health checks de database

---

## Task 7: Mensageria com RabbitMQ 📋
**Status**: Planejada

### Descrição
Implementar comunicação assíncrona entre microsserviços usando RabbitMQ.

### Entregáveis Planejados
- [ ] MassTransit/RabbitMQ.Client
- [ ] Publicadores de eventos
- [ ] Consumers de eventos
- [ ] Configuração de exchanges e queues
- [ ] Health checks de mensageria

---

## Legenda
- ✅ Concluída
- 🚧 Em Progresso
- 📋 Planejada
- ❌ Bloqueada
