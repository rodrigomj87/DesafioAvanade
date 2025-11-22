using Auth.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

public sealed class RefreshTokenService
{
    private readonly AuthDbContext _context;
    private readonly TokenService _tokenService;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        AuthDbContext context,
        TokenService tokenService,
        ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(string userId, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var refreshToken = new RefreshToken(userId, token, expiresAt, deviceId);

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token created for user {UserId} with expiration {ExpiresAt}", userId, expiresAt);

        return refreshToken;
    }

    public async Task<(bool IsValid, RefreshToken? Token, string? Error)> ValidateRefreshTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        if (refreshToken is null)
        {
            _logger.LogWarning("Refresh token not found");
            return (false, null, "Invalid refresh token");
        }

        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token {TokenId} is revoked", refreshToken.Id);
            return (false, null, "Token has been revoked");
        }

        if (refreshToken.IsExpired)
        {
            _logger.LogWarning("Refresh token {TokenId} is expired", refreshToken.Id);
            return (false, null, "Token has expired");
        }

        return (true, refreshToken, null);
    }

    public async Task<string> RefreshAccessTokenAsync(string refreshTokenValue, CancellationToken cancellationToken = default)
    {
        var (isValid, refreshToken, error) = await ValidateRefreshTokenAsync(refreshTokenValue, cancellationToken);

        if (!isValid || refreshToken is null)
        {
            throw new InvalidOperationException(error ?? "Invalid refresh token");
        }

        var roles = new[] { "inventory.read", "inventory.write", "sales.read", "sales.write" };
        var tokenResult = _tokenService.CreateToken(refreshToken.UserId, roles);

        _logger.LogInformation("Access token refreshed for user {UserId}", refreshToken.UserId);

        return tokenResult.AccessToken;
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

        if (refreshToken is null)
        {
            throw new InvalidOperationException("Refresh token not found");
        }

        refreshToken.Revoke();
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token {TokenId} revoked for user {UserId}", refreshToken.Id, refreshToken.UserId);
    }

    public async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoffDate)
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count > 0)
        {
            _context.RefreshTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);
        }
    }
}
