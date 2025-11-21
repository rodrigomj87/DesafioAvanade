# Desafio Avanade - Documento Base

## Visão Geral

Este projeto implementa uma plataforma de e-commerce baseada em microsserviços, demonstrando boas práticas de arquitetura, segurança e observabilidade.

## Arquitetura

### Microsserviços

1. **ApiGateway** (YARP-based)
   - Roteamento reverso para microsserviços
   - Autenticação JWT com suporte a JWKS
   - Rate limiting por IP
   - OpenTelemetry para traces e métricas
   - Health checks públicos
   - Correlation ID para rastreamento de requisições

2. **AuthService** (Auth.Api)
   - Serviço de autenticação stub
   - Geração de tokens JWT RS256
   - Endpoint JWKS (/.well-known/jwks.json)
   - Suporte a roles e claims customizados

3. **InventoryService**
   - Gerenciamento de inventário/produtos
   - Arquitetura em camadas (Api/Application/Domain/Infrastructure)
   - Health checks

4. **SalesService**
   - Gerenciamento de vendas
   - Arquitetura em camadas (Api/Application/Domain/Infrastructure)
   - Health checks

### Infraestrutura

- **SQL Server** (via Docker Compose)
- **RabbitMQ** (via Docker Compose)
- **.NET 10.0**
- **GitHub Actions** para CI/CD

## Segurança

### Autenticação JWT

O sistema utiliza JWT (JSON Web Tokens) com algoritmo RS256 para autenticação:

- **Emissor (Issuer)**: Configurável via `Jwt:Issuer`
- **Audiência (Audience)**: Configurável via `Jwt:Audience`
- **Chave Pública**: Carregada via JWKS (inline ou remoto)

#### Modos de Carregamento JWKS

1. **Inline** (padrão): Chave pública definida em `appsettings.json`
2. **Remote**: Busca JWKS de um endpoint externo (ex: Auth Service)

### Rate Limiting

Proteção contra abuso de APIs:
- **Padrão**: 30 requisições a cada 10 segundos por IP
- Aplicado apenas às rotas proxy (/inventory, /sales)
- Health checks não são limitados

## Observabilidade

### OpenTelemetry

Instrumentação completa com:
- **Traces**: AspNetCore, HttpClient
- **Métricas**: AspNetCore, HttpClient, Runtime
- **Exporters**: Console (desenvolvimento)

### Logging

- **Serilog** com formato JSON estruturado
- Correlation IDs em todos os logs
- Request logging automático

### Health Checks

- `GET /health` - Status básico
- `GET /healthz` - Status com timestamp

## Estrutura do Projeto

```
src/
  ApiGateway/                     -> Gateway YARP
    Extensions/                   -> Service extensions
    Configurations/               -> JWT e Rate Limiting
  AuthService/Auth.Api/           -> Serviço de autenticação
  Services/
    InventoryService/
      Inventory.Api/              -> API pública
      Inventory.Application/      -> Lógica de aplicação
      Inventory.Domain/           -> Entidades e interfaces
      Inventory.Infrastructure/   -> Repositórios
    SalesService/
      Sales.Api/                  -> API pública
      Sales.Application/          -> Lógica de aplicação
      Sales.Domain/               -> Entidades
      Sales.Infrastructure/       -> Repositórios

tests/
  ApiGateway.Tests/               -> Testes do gateway

infra/
  dev/                            -> Docker Compose (dev)

Docs/
  tasks/                          -> Backlog e sprints
  ADRs/                           -> Architecture Decision Records
```

## Próximas Etapas

1. Implementar integrações com SQL Server
2. Implementar mensageria com RabbitMQ
3. Adicionar mais endpoints de negócio
4. Configurar GHCR para publicação de imagens Docker
5. Implementar testes de integração
6. Adicionar documentação ADR para decisões arquiteturais
