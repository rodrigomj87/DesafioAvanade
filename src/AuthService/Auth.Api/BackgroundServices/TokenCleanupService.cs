using Auth.Api.Services;

namespace Auth.Api.BackgroundServices;

public sealed class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public TokenCleanupService(IServiceProvider serviceProvider, ILogger<TokenCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var refreshTokenService = scope.ServiceProvider.GetRequiredService<RefreshTokenService>();

                await refreshTokenService.CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Token cleanup service is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired tokens");
            }
        }

        _logger.LogInformation("Token cleanup service stopped");
    }
}
