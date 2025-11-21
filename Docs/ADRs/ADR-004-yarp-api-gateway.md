# ADR-004: YARP como API Gateway

**Status**: Aceito  
**Data**: 2025-11-19  
**Decisores**: Equipe de Arquitetura

## Contexto

Precisamos de um API Gateway para:
- Roteamento centralizado para microsserviços
- Autenticação/autorização centralizada
- Rate limiting
- Observabilidade
- Single entry point para clientes

## Decisão

Usar YARP (Yet Another Reverse Proxy) da Microsoft como solução de API Gateway:

1. **Características do YARP**
   - Biblioteca .NET nativa
   - Alto desempenho
   - Configuração flexível (código ou arquivo)
   - Integração com middleware ASP.NET Core

2. **Funcionalidades Implementadas**
   - Reverse proxy para Inventory e Sales services
   - Autenticação JWT centralizada
   - Rate limiting por IP
   - Health checks
   - Correlation IDs
   - OpenTelemetry

3. **Configuração**
   - Provider customizado (GatewayProxyConfigProvider)
   - Rotas configuradas dinamicamente via appsettings
   - Transformações de requests/responses quando necessário

## Consequências

### Positivas
- ✅ Performance: Desenvolvido pela Microsoft, otimizado para .NET
- ✅ Integração nativa: Usa middleware ASP.NET Core
- ✅ Flexibilidade: Configuração por código ou configuração
- ✅ Gratuito: Open source
- ✅ Suporte: Mantido pela Microsoft
- ✅ Extensível: Fácil adicionar comportamentos customizados

### Negativas
- ❌ Menos features out-of-box que gateways dedicados (Kong, Apigee)
- ❌ Específico para .NET (não multi-linguagem como Envoy)
- ❌ Menos maduro que alternativas estabelecidas

## Alternativas Consideradas

1. **Ocelot**: Gateway .NET mais antigo, mas menos performático e com menos suporte
2. **Kong**: Muito robusto, mas adiciona dependência externa e complexidade
3. **Envoy**: Padrão de mercado, mas mais complexo e não .NET-nativo
4. **Azure API Management**: Solução gerenciada, mas vendor lock-in e custos
5. **AWS API Gateway**: Solução gerenciada, mas vendor lock-in e custos

## Notas de Implementação

### Extensions Pattern
Código organizado em extensions para manter Program.cs limpo:
- `AddGatewayObservability()`
- `AddGatewayAuthentication()`
- `AddGatewayRateLimiting()`
- `AddGatewayReverseProxy()`
- `UseGatewayPipeline()`

### Rotas
- `/inventory/{**catch-all}` → Inventory Service
- `/sales/{**catch-all}` → Sales Service
- `/health`, `/healthz` → Health checks (sem autenticação, sem rate limit)
