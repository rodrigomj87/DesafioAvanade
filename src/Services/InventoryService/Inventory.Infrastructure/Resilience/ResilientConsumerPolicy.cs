using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Inventory.Infrastructure.Resilience;

public sealed class ResilientConsumerPolicy
{
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
    private readonly ILogger<ResilientConsumerPolicy> _logger;

    public ResilientConsumerPolicy(ILogger<ResilientConsumerPolicy> logger)
    {
        _logger = logger;

        _circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(exception,
                        "Circuit breaker aberto. Duração: {Duration}s. Exceção: {ExceptionMessage}",
                        duration.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker fechado. Sistema operando normalmente");
                },
                onHalfOpen: () =>
                {
                    _logger.LogWarning("Circuit breaker em modo half-open. Testando recuperação");
                });
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action)
    {
        return await _circuitBreakerPolicy.ExecuteAsync(action);
    }

    public async Task ExecuteAsync(Func<Task> action)
    {
        await _circuitBreakerPolicy.ExecuteAsync(action);
    }

    public bool IsCircuitOpen => _circuitBreakerPolicy.CircuitState == CircuitState.Open;
    public bool IsCircuitHalfOpen => _circuitBreakerPolicy.CircuitState == CircuitState.HalfOpen;
}
