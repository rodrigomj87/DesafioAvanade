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
using OpenTelemetry.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Net.Http;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;
using Shared.Observability;

namespace Microsoft.Extensions.DependencyInjection;

internal static class GatewayServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var openTelemetryBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => OtlpConfigurator.ConfigureOtlpExporterForTraces(configuration, options));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(options => OtlpConfigurator.ConfigureOtlpExporterForMetrics(configuration, options));
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

    // OTLP configuration moved to Shared.Observability.OtlpConfigurator
}
