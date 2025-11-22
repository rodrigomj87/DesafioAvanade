namespace Auth.Api;

public sealed class AuthOptions
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int TokenLifetimeMinutes { get; set; } = 60;
    public List<RsaKeyConfig> RsaKeys { get; set; } = new();
}

public sealed class RsaKeyConfig
{
    public string KeyId { get; set; } = string.Empty;
    public string RsaKeyXml { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
