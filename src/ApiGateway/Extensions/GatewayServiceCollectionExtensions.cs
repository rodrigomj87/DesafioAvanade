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
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services, string serviceName)
    {
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

        services.AddAuthorization();

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
}
