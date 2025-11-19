using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using ApiGateway;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddSingleton(provider => JwtConfigurationBuilder.BuildJsonWebKeySet(
    provider.GetRequiredService<IConfiguration>(),
    provider.GetRequiredService<IHttpClientFactory>()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, JsonWebKeySet>((options, configuration, jwks) =>
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.Keys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddReverseProxy();
builder.Services.AddSingleton<IProxyConfigProvider, GatewayProxyConfigProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy().RequireAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "UP" })).AllowAnonymous();

app.Run();

public partial class Program;

internal static class JwtConfigurationBuilder
{
    public static JsonWebKeySet BuildJsonWebKeySet(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        var jwksMode = (configuration["Jwt:JwksMode"] ?? "Inline").Trim();

        if (string.Equals(jwksMode, "Remote", StringComparison.OrdinalIgnoreCase))
        {
            return FetchRemoteJwks(configuration, httpClientFactory);
        }

        return BuildInlineJwks(configuration);
    }

    private static JsonWebKeySet BuildInlineJwks(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        if (!jwtSection.Exists())
        {
            throw new InvalidOperationException("Jwt configuration section is missing.");
        }

        var jwksConfig = jwtSection.GetSection("Jwks").Get<JsonWebKeyConfiguration>();
        if (jwksConfig is null || jwksConfig.Keys is null || jwksConfig.Keys.Count == 0)
        {
            throw new InvalidOperationException("Jwt:Jwks configuration is invalid or empty.");
        }

        var json = JsonSerializer.Serialize(jwksConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new JsonWebKeySet(json);
    }

    private static JsonWebKeySet FetchRemoteJwks(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        var endpoint = configuration["Jwt:JwksEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Jwt:JwksEndpoint deve ser informado quando Jwt:JwksMode = Remote.");
        }

        var client = httpClientFactory.CreateClient("jwks");
        client.Timeout = TimeSpan.FromSeconds(5);
        var json = client.GetStringAsync(endpoint).GetAwaiter().GetResult();

        return new JsonWebKeySet(json);
    }
}

internal sealed class JsonWebKeyConfiguration
{
    public IList<JsonWebKeyEntry> Keys { get; set; } = new List<JsonWebKeyEntry>();
}

internal sealed class JsonWebKeyEntry
{
    public string? Kty { get; set; }
    public string? Use { get; set; }
    public string? Alg { get; set; }
    public string? Kid { get; set; }
    public string? N { get; set; }
    public string? E { get; set; }
}
