using ApiGateway.Configurations;
using ApiGateway;
using ApiGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Net.Http;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

internal static class GatewayServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => ConfigureOtlpExporter(configuration, options));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(options => ConfigureOtlpExporter(configuration, options));
            });

        return services;
    }

    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<JwksHolder>();
        services.AddHostedService<JwksBackgroundService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, JwksHolder>((options, config, jwksHolder) =>
            {
                var issuer = config["Jwt:Issuer"]
                    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
                var audience = config["Jwt:Audience"];

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = jwksHolder.GetJwks().Keys,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("GatewayAuthenticated", policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy("AllowAnonymous", policy =>
            {
                policy.RequireAssertion(_ => true);
            });
        });

        return services;
    }

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingSettings>(configuration.GetSection("RateLimiting"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("gateway-default", httpContext =>
            {
                var settings = httpContext.RequestServices
                    .GetRequiredService<IOptionsMonitor<RateLimitingSettings>>()
                    .CurrentValue;
                var normalized = RateLimitingSettings.Normalize(settings);
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = normalized.PermitLimit,
                    Window = TimeSpan.FromSeconds(normalized.WindowSeconds),
                    QueueLimit = normalized.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }

    public static IServiceCollection AddGatewayReverseProxy(this IServiceCollection services)
    {
        services.AddReverseProxy();
        services.AddSingleton<IProxyConfigProvider, GatewayProxyConfigProvider>();
        return services;
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
