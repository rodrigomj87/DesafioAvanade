using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Context;
using System;
using System.Linq;

namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{
    private const string CorrelationHeaderName = "X-Correlation-ID";

    public static WebApplication UseGatewayDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[CorrelationHeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
                context.Request.Headers[CorrelationHeaderName] = correlationId;
            }

            context.Items[CorrelationHeaderName] = correlationId;
            context.Response.Headers[CorrelationHeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next().ConfigureAwait(false);
            }
        });

        return app;
    }

    public static WebApplication UseGatewayRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                if (httpContext.Items.TryGetValue(CorrelationHeaderName, out var correlation))
                {
                    diagnosticContext.Set("CorrelationId", correlation);
                }

                diagnosticContext.Set("RequestPath", httpContext.Request.Path);
                diagnosticContext.Set("ResponseStatusCode", httpContext.Response.StatusCode);
            };
        });

        return app;
    }

    public static WebApplication MapGatewayHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "UP" }))
            .AllowAnonymous()
            .DisableRateLimiting();

        app.MapGet("/healthz", () => Results.Ok(new { status = "UP", timestamp = DateTimeOffset.UtcNow }))
            .AllowAnonymous()
            .DisableRateLimiting();

        return app;
    }

    public static IEndpointConventionBuilder MapGatewayReverseProxy(this WebApplication app)
    {
        return app.MapReverseProxy()
            .RequireRateLimiting("gateway-default");
    }
}
