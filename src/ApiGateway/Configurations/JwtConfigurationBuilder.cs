using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Configurations;

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
        
        const int maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var json = client.GetStringAsync(endpoint).GetAwaiter().GetResult();
                return new JsonWebKeySet(json);
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                Thread.Sleep(delay);
                delay = delay.Add(TimeSpan.FromSeconds(1));
            }
        }
        
        throw new InvalidOperationException($"Não foi possível buscar JWKS de {endpoint} após {maxRetries} tentativas.");
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
