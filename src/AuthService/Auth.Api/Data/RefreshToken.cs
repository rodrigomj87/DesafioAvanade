namespace Auth.Api.Data;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? DeviceId { get; private set; }

    private RefreshToken() { }

    public RefreshToken(string userId, string token, DateTime expiresAt, string? deviceId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Token = token ?? throw new ArgumentNullException(nameof(token));
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        DeviceId = deviceId;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsValid => !IsExpired && !IsRevoked;

    public void Revoke()
    {
        if (IsRevoked)
            throw new InvalidOperationException("Token already revoked");

        RevokedAt = DateTime.UtcNow;
    }
}
