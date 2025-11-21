# ADR-005: OpenTelemetry para Observabilidade

**Status**: Aceito  
**Data**: 2025-11-19  
**Decisores**: Equipe de Arquitetura, DevOps

## Contexto

Microsserviços distribuídos requerem observabilidade robusta para:
- Rastreamento de requisições entre serviços
- Métricas de performance e saúde
- Debugging de problemas em produção
- Análise de comportamento do sistema

## Decisão

Adotar OpenTelemetry como padrão para observabilidade:

1. **Instrumentação**
   - **Traces**: Rastreamento distribuído de requisições
     - AspNetCore: Requisições HTTP recebidas
     - HttpClient: Requisições HTTP enviadas
   - **Métricas**: Coleta de métricas do sistema
     - AspNetCore: Request duration, status codes
     - HttpClient: Request duration, falhas
     - Runtime: GC, threads, memória, exceções

2. **Correlation IDs**
   - Header `X-Correlation-ID` em todas as requisições
   - Propagação automática para serviços downstream
   - Inclusão em logs via Serilog

3. **Logging Estruturado**
   - Serilog com formato JSON
   - Request logging automático
   - Enrichment com Correlation ID
   - Saída para console (desenvolvimento)

4. **Exporters**
   - Console exporter (desenvolvimento)
   - Preparado para OTLP, Jaeger, Zipkin (produção)

## Consequências

### Positivas
- ✅ Padrão da indústria: OpenTelemetry é vendor-neutral
- ✅ Flexibilidade: Fácil trocar backends (Jaeger, Zipkin, etc.)
- ✅ Instrumentação automática: Menos código boilerplate
- ✅ Traces distribuídos: Visibilidade end-to-end
- ✅ Correlation IDs: Rastreamento fácil de requisições
- ✅ Logs estruturados: Fácil de consultar e analisar

### Negativas
- ❌ Overhead de performance (mínimo)
- ❌ Complexidade inicial de setup
- ❌ Necessita backend de coleta (Jaeger, etc.) em produção

## Implementação

### API Gateway
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());
```

### Correlation ID Middleware
```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Items["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

## Roadmap

### Desenvolvimento
- [x] Console exporter para traces
- [x] Console exporter para métricas
- [x] Correlation IDs
- [x] Logs estruturados JSON

### Produção (Futuro)
- [ ] Jaeger/Zipkin para traces
- [ ] Prometheus para métricas
- [ ] Grafana para visualização
- [ ] ELK/Loki para logs centralizados
- [ ] Alerting baseado em métricas

## Alternativas Consideradas

1. **Application Insights**: Excelente, mas vendor lock-in (Azure)
2. **Prometheus + Grafana**: Focado em métricas, menos completo para traces
3. **ELK Stack**: Focado em logs, sem traces nativamente
4. **Instrumentação manual**: Muito trabalho, propenso a erros
