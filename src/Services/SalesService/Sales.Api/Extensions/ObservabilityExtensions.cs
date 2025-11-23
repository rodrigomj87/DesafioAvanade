using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sales.Infrastructure.Observability;
using Shared.Observability;
using Serilog;
using Serilog.Formatting.Json;
using System;

namespace Sales.Api.Extensions;

internal static class ObservabilityExtensions
{
    public static WebApplicationBuilder ConfigureSalesLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "SalesService")
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Console(formatter: new JsonFormatter());
        });

        return builder;
    }

    public static IServiceCollection AddSalesObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration.GetValue<string>("ServiceName") ?? "SalesService";
        var serviceVersion = configuration.GetValue<string>("ServiceVersion") ?? "1.0.0";

        var openTelemetryBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(SalesTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => OtlpConfigurator.ConfigureOtlpExporterForTraces(configuration, options));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(SalesTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(options => OtlpConfigurator.ConfigureOtlpExporterForMetrics(configuration, options));
            });

        return services;
    }

    public static WebApplication UseSalesRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme ?? string.Empty);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString() ?? string.Empty);

                if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
                {
                    diagnosticContext.Set("CorrelationId", correlationId.ToString() ?? string.Empty);
                }
            };
        });

        return app;
    }

    // OTLP configuration moved to Shared.Observability.OtlpConfigurator
}
