using Auth.Api;
using Auth.Api.Data;
using Auth.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Auth.Api.Tests;

public sealed class RefreshTokenServiceTests : IDisposable
{
    private readonly AuthDbContext _context;
    private readonly RefreshTokenService _service;
    private readonly TokenService _tokenService;
    private readonly Mock<ILogger<RefreshTokenService>> _loggerMock;

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AuthDbContext(options);
        _loggerMock = new Mock<ILogger<RefreshTokenService>>();

        var authOptions = Options.Create(new AuthOptions
        {
            Issuer = "https://test.local",
            Audience = "test-audience",
            TokenLifetimeMinutes = 60,
            KeyId = "test-key"
        });

        _tokenService = new TokenService(authOptions);
        _service = new RefreshTokenService(_context, _tokenService, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateRefreshTokenAsync_ShouldCreateTokenWithCorrectExpiration()
    {
        var userId = "test-user";
        var deviceId = "device-123";

        var refreshToken = await _service.CreateRefreshTokenAsync(userId, deviceId);

        refreshToken.Should().NotBeNull();
        refreshToken.UserId.Should().Be(userId);
        refreshToken.DeviceId.Should().Be(deviceId);
        refreshToken.Token.Should().NotBeNullOrEmpty();
        refreshToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        refreshToken.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        refreshToken.RevokedAt.Should().BeNull();

        var savedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == refreshToken.Id);
        savedToken.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRefreshTokenAsync_WithoutDeviceId_ShouldCreateTokenSuccessfully()
    {
        var userId = "test-user-2";

        var refreshToken = await _service.CreateRefreshTokenAsync(userId);

        refreshToken.Should().NotBeNull();
        refreshToken.UserId.Should().Be(userId);
        refreshToken.DeviceId.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WithValidToken_ShouldReturnNewAccessToken()
    {
        var userId = "test-user-3";
        var refreshToken = await _service.CreateRefreshTokenAsync(userId);

        var accessToken = await _service.RefreshAccessTokenAsync(refreshToken.Token);

        accessToken.Should().NotBeNullOrEmpty();
        accessToken.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WithExpiredToken_ShouldThrowException()
    {
        var userId = "test-user-4";
        var expiredToken = new RefreshToken(userId, "expired-token", DateTime.UtcNow.AddDays(-1));
        _context.RefreshTokens.Add(expiredToken);
        await _context.SaveChangesAsync();

        var act = async () => await _service.RefreshAccessTokenAsync(expiredToken.Token);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Token has expired");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WithRevokedToken_ShouldThrowException()
    {
        var userId = "test-user-5";
        var refreshToken = await _service.CreateRefreshTokenAsync(userId);
        await _service.RevokeRefreshTokenAsync(refreshToken.Token);

        var act = async () => await _service.RefreshAccessTokenAsync(refreshToken.Token);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Token has been revoked");
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_WithNonExistentToken_ShouldThrowException()
    {
        var act = async () => await _service.RefreshAccessTokenAsync("non-existent-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid refresh token");
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WithValidToken_ShouldMarkAsRevoked()
    {
        var userId = "test-user-6";
        var refreshToken = await _service.CreateRefreshTokenAsync(userId);

        await _service.RevokeRefreshTokenAsync(refreshToken.Token);

        var revokedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == refreshToken.Id);
        revokedToken.Should().NotBeNull();
        revokedToken!.RevokedAt.Should().NotBeNull();
        revokedToken.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        revokedToken.IsRevoked.Should().BeTrue();
        revokedToken.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WithNonExistentToken_ShouldThrowException()
    {
        var act = async () => await _service.RevokeRefreshTokenAsync("non-existent-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Refresh token not found");
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithValidToken_ShouldReturnTrue()
    {
        var userId = "test-user-7";
        var refreshToken = await _service.CreateRefreshTokenAsync(userId);

        var (isValid, token, error) = await _service.ValidateRefreshTokenAsync(refreshToken.Token);

        isValid.Should().BeTrue();
        token.Should().NotBeNull();
        token!.UserId.Should().Be(userId);
        error.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithExpiredToken_ShouldReturnFalse()
    {
        var userId = "test-user-8";
        var expiredToken = new RefreshToken(userId, "expired-token-2", DateTime.UtcNow.AddDays(-1));
        _context.RefreshTokens.Add(expiredToken);
        await _context.SaveChangesAsync();

        var (isValid, token, error) = await _service.ValidateRefreshTokenAsync(expiredToken.Token);

        isValid.Should().BeFalse();
        token.Should().BeNull();
        error.Should().Be("Token has expired");
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithRevokedToken_ShouldReturnFalse()
    {
        var userId = "test-user-9";
        var refreshToken = await _service.CreateRefreshTokenAsync(userId);
        await _service.RevokeRefreshTokenAsync(refreshToken.Token);

        var (isValid, token, error) = await _service.ValidateRefreshTokenAsync(refreshToken.Token);

        isValid.Should().BeFalse();
        token.Should().BeNull();
        error.Should().Be("Token has been revoked");
    }

    [Fact]
    public async Task CleanupExpiredTokensAsync_ShouldRemoveOldExpiredTokens()
    {
        var userId = "test-user-10";
        
        var oldExpiredToken = new RefreshToken(userId, "old-expired-1", DateTime.UtcNow.AddDays(-31));
        var recentExpiredToken = new RefreshToken(userId, "recent-expired", DateTime.UtcNow.AddDays(-1));
        var validToken = new RefreshToken(userId, "valid-token", DateTime.UtcNow.AddDays(7));

        _context.RefreshTokens.AddRange(oldExpiredToken, recentExpiredToken, validToken);
        await _context.SaveChangesAsync();

        await _service.CleanupExpiredTokensAsync();

        var remainingTokens = await _context.RefreshTokens.ToListAsync();
        remainingTokens.Should().HaveCount(2);
        remainingTokens.Should().NotContain(t => t.Id == oldExpiredToken.Id);
        remainingTokens.Should().Contain(t => t.Id == recentExpiredToken.Id);
        remainingTokens.Should().Contain(t => t.Id == validToken.Id);
    }

    [Fact]
    public void RefreshToken_IsValid_ShouldReturnFalseWhenExpired()
    {
        var expiredToken = new RefreshToken("user-1", "token-1", DateTime.UtcNow.AddDays(-1));

        expiredToken.IsValid.Should().BeFalse();
        expiredToken.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_IsValid_ShouldReturnFalseWhenRevoked()
    {
        var token = new RefreshToken("user-2", "token-2", DateTime.UtcNow.AddDays(7));
        token.Revoke();

        token.IsValid.Should().BeFalse();
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_Revoke_ShouldThrowWhenAlreadyRevoked()
    {
        var token = new RefreshToken("user-3", "token-3", DateTime.UtcNow.AddDays(7));
        token.Revoke();

        var act = () => token.Revoke();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Token already revoked");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
