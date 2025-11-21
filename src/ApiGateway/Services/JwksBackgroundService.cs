using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ApiGateway.Services;

internal sealed class JwksBackgroundService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwksBackgroundService> _logger;
    private readonly JwksHolder _jwksHolder;

    public JwksBackgroundService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JwksBackgroundService> logger,
        JwksHolder jwksHolder)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _jwksHolder = jwksHolder;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoint = _configuration["Jwt:JwksEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Jwt:JwksEndpoint não configurado. JWKS não será carregado.");
            return;
        }

        _logger.LogInformation("Iniciando fetch de JWKS de {Endpoint}", endpoint);
        
        const int maxRetries = 10;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxRetries && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("jwks");
                client.Timeout = TimeSpan.FromSeconds(5);
                
                var json = await client.GetStringAsync(endpoint, stoppingToken);
                var jwks = new JsonWebKeySet(json);
                
                _jwksHolder.SetJwks(jwks);
                
                _logger.LogInformation("✅ JWKS carregado com sucesso de {Endpoint} (tentativa {Attempt}/{MaxRetries})", 
                    endpoint, attempt, maxRetries);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, 
                    "Falha ao buscar JWKS de {Endpoint} (tentativa {Attempt}/{MaxRetries}). Aguardando {Delay}s...", 
                    endpoint, attempt, maxRetries, delay.TotalSeconds);
                
                await Task.Delay(delay, stoppingToken);
                delay += TimeSpan.FromSeconds(1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "❌ Falha DEFINITIVA ao buscar JWKS de {Endpoint} após {MaxRetries} tentativas.", 
                    endpoint, maxRetries);
                throw;
            }
        }
    }
}

internal sealed class JwksHolder
{
    private JsonWebKeySet? _jwks;
    private readonly object _lock = new();

    public void SetJwks(JsonWebKeySet jwks)
    {
        lock (_lock)
        {
            _jwks = jwks;
        }
    }

    public JsonWebKeySet GetJwks()
    {
        lock (_lock)
        {
            if (_jwks is null)
            {
                throw new InvalidOperationException("JWKS ainda não foi carregado. Aguarde o startup.");
            }
            return _jwks;
        }
    }

    public bool IsLoaded()
    {
        lock (_lock)
        {
            return _jwks is not null;
        }
    }
}
