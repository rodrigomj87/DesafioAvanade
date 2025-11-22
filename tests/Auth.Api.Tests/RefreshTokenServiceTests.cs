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
            RsaKeys = new List<RsaKeyConfig>
            {
                new()
                {
                    KeyId = "test-key",
                    RsaKeyXml = "<RSAKeyValue><Modulus>luHEb58gQva+YXuHrX15WKPAlkAsy8IHtXgGz3Gbj34qtwhkiwJLlVqyqK3oh9Gc9QUlmXsjl/8iWRKsBrh7el962z2zkKZpAa85hnDmomc2Xxq2cg8WbvSy/95EE7uTEKFsFdYMNnMbTVHn3bSWhgvR/OqPq9NH2lOQfqX86PBGa8cFk6tZ88rQPKq7qhDDTtiS15yBRXwamNQluEF0XYOAXXp8CBGwpYH9uG1drBT0fGQxcN7syDIt0KQYVw6oMkhZcGVU/R3jBzq6vDlYqx4yPwqg6vuex65LycGkJSajEdy8ATZO19MlYUiOcaHrUVdl2vrF/p6fisyyIlDFSQ==</Modulus><Exponent>AQAB</Exponent><P>yHCU9yjrqY7c052bZ/l+Rbi8pqrZBejkyz9XBNWOwHwOLOY5zW/khdlhdsXZyBREPNtOJtND7TOZgEJVkSJoPVzF6+Urte+yrgcy2mdY4Yiuj5brtH16trr9UcIBvGWgQZqjC0684ZWr9Q9FZv98mInR3oWHvRUTeb+B6Eed9V8=</P><Q>wLSBs811WpImCjCRC91Tpo/iFndEvEns/9Ondec1U4LXr5orVx7rbMI6kPsq+hqn72I7rsjtogqbh/BfQzVvhBAGmI7P9KtalFqFRHh6WDE/ucrDoRYLGNidiPKrM07rvwjt1wfDstL6PePUQDq7n5lJORDA/snDytfAHxNm3lc=</Q><DP>vHW+WrXK/t4om3/xAs3/ACc2ma7B52CW3tNrSkxRsIH0Ahtl9ddjEsbVcQuOPSVxE5dP4Al8MKb3iTTVp2SmhiuYnLgZZTM+2YpDMj9Bvqz5FGFetZHVs8IVNK4pW65iA80VyCf8SE6mJAB7zOAjknjjL0p12j7a1PpPdsr/phk=</DP><DQ>d52UBO+7sISXaCvNXPf9PJ6DUD6oQ9f/bK0MfXsO9s944l9oF6PxrojBoczGWIjDRSxMr6m5F6RN9ouWFjnNBsSL05aSLuVOTFgD8QR3rNalH8ZkuFDegoWzA5bd/fM3ElWQjLEdIJslCdWpolTSsbaMZQepRH4pMnfJoYCBkfE=</DQ><InverseQ>NaLj/DLRIb+VpvQWv2xyFu5SQGDHX++iU8x8Ph9HDpCcBdo1QHQ1hklloIOO6DeFvTVAAh5Nf+VNV5lkIKdB1ndqlN89n94wPGcrZqEaoQdyu/LD3GzkCQb5jIAzQxeBoXnH46Hc0q4c4oWmBnur8+qXFPbT1WQy+myJMK8Foho=</InverseQ><D>NTYwOhGLpjIxGF4hjo0G47z0qci+Q6oNX/GRrU1qyGN9mwmJtAC+OVFAWv7iWCwTx9Hl3cgw2GUxbuqH4uLjIVhVEsci8evOcrqidgiRPktaQJZCK92oUr9X9ackqTTo+9+CJ/1SV02nHlC5jlYnE+VNTH5I5xdfat83wYK2vEueUzTU8E4MEIdVt6Qvno+188sn1Jd7uZB49UFSyNkByLyd0tW5ANmvtPv2EChDVhOw84G9CzeNGqI9xwJgH7z0DEfM6Kw3W9hzX4u9fjBBz1vPK2oMGy9AQSRuKDoiHOhcSrvTa0ImGxll8nf2MXPErpS0IcQMkAKVz/hinGDrNQ==</D></RSAKeyValue>",
                    IsPrimary = true
                }
            }
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
