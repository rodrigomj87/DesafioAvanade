using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Inventory.Infrastructure.Observability;
using Serilog;
using Serilog.Formatting.Json;
using System;

namespace Inventory.Api.Extensions;

internal static class ObservabilityExtensions
{
    public static WebApplicationBuilder ConfigureInventoryLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "InventoryService")
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .WriteTo.Console(formatter: new JsonFormatter());
        });

        return builder;
    }

    public static IServiceCollection AddInventoryObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration.GetValue<string>("ServiceName") ?? "InventoryService";
        var serviceVersion = configuration.GetValue<string>("ServiceVersion") ?? "1.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(InventoryTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => ConfigureOtlpExporter(configuration, options));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(InventoryTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(options => ConfigureOtlpExporter(configuration, options));
            });

        return services;
    }

    public static WebApplication UseInventoryRequestLogging(this WebApplication app)
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

    private static void ConfigureOtlpExporter(IConfiguration configuration, OtlpExporterOptions options)
    {
        var otlpSection = configuration.GetSection("OpenTelemetry:Otlp");

        if (otlpSection.Exists())
        {
            var endpoint = otlpSection.GetValue<string>("Endpoint");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }

            var headers = otlpSection.GetValue<string>("Headers");
            if (!string.IsNullOrWhiteSpace(headers))
            {
                options.Headers = headers;
            }

            var protocol = otlpSection.GetValue<string>("Protocol");
            if (!string.IsNullOrWhiteSpace(protocol) && Enum.TryParse<OtlpExportProtocol>(protocol, true, out var parsedProtocol))
            {
                options.Protocol = parsedProtocol;
            }
        }

        options.Endpoint ??= new Uri("http://localhost:4317");
    }
}
