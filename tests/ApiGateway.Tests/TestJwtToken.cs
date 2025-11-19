using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Tests;

internal static class TestJwtToken
{
    public const string Issuer = "https://test-auth.local";
    public const string Audience = "desafio-avanade";
    public const string KeyId = "test-key";
    private static readonly RsaSecurityKey SigningKey;
    private static readonly JsonWebKey PublicJwk;

    static TestJwtToken()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);
        SigningKey = new RsaSecurityKey(parameters) { KeyId = KeyId };

        var publicParameters = new RSAParameters
        {
            Modulus = parameters.Modulus,
            Exponent = parameters.Exponent
        };

        var publicKey = new RsaSecurityKey(publicParameters) { KeyId = KeyId };
        PublicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
    }

    public static string CreateValidToken(DateTimeOffset? expires = null)
        => CreateToken(DateTimeOffset.UtcNow.AddMinutes(-1), expires ?? DateTimeOffset.UtcNow.AddMinutes(5));

    public static string CreateExpiredToken()
        => CreateToken(DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5));

    private static string CreateToken(DateTimeOffset notBefore, DateTimeOffset expires)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim("roles", "inventory.read")
            }),
            NotBefore = notBefore.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials
        };

        return handler.CreateEncodedJwt(descriptor);
    }

    public static IEnumerable<KeyValuePair<string, string?>> BuildConfiguration(string inventoryAddress, string salesAddress)
    {
        yield return new KeyValuePair<string, string?>("Services:Inventory", inventoryAddress);
        yield return new KeyValuePair<string, string?>("Services:Sales", salesAddress);
        yield return new KeyValuePair<string, string?>("Jwt:Issuer", Issuer);
        yield return new KeyValuePair<string, string?>("Jwt:Audience", Audience);
        yield return new KeyValuePair<string, string?>("Jwt:JwksMode", "Inline");
        yield return new KeyValuePair<string, string?>("RateLimiting:PermitLimit", "2");
        yield return new KeyValuePair<string, string?>("RateLimiting:WindowSeconds", "60");
        yield return new KeyValuePair<string, string?>("RateLimiting:QueueLimit", "0");
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:kty", "RSA");
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:use", "sig");
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:alg", SecurityAlgorithms.RsaSha256);
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:kid", KeyId);
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:n", PublicJwk.N);
        yield return new KeyValuePair<string, string?>("Jwt:Jwks:keys:0:e", PublicJwk.E);
    }
}
