namespace Auth.Api;

public sealed class AuthOptions
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public int TokenLifetimeMinutes { get; set; } = 60;
    public string? KeyId { get; set; }
    public string? RsaKeyXml { get; set; }
}
