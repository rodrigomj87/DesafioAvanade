using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api;

internal sealed class TokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _credentials;
    private readonly JsonWebKey _publicJsonWebKey;
    private readonly TimeSpan _tokenLifetime;
    private readonly string _issuer;
    private readonly string? _audience;

    public TokenService(IOptions<AuthOptions> options)
    {
        var settings = options.Value;
        _issuer = settings.Issuer ?? throw new InvalidOperationException("Auth:Issuer não configurado.");
        _audience = settings.Audience;
        _tokenLifetime = TimeSpan.FromMinutes(settings.TokenLifetimeMinutes > 0 ? settings.TokenLifetimeMinutes : 60);

        var keyId = settings.KeyId ?? "auth-stub";

        var rsa = RSA.Create(2048);
        
        if (!string.IsNullOrWhiteSpace(settings.RsaKeyXml))
        {
            rsa.FromXmlString(settings.RsaKeyXml);
        }

        var parameters = rsa.ExportParameters(true);

        var signingKey = new RsaSecurityKey(parameters)
        {
            KeyId = keyId
        };

        _credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var publicParameters = new RSAParameters
        {
            Modulus = parameters.Modulus,
            Exponent = parameters.Exponent
        };

        var publicKey = new RsaSecurityKey(publicParameters)
        {
            KeyId = keyId
        };

        _publicJsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
    }

    public AuthTokenResult CreateToken(string subject, IEnumerable<string>? roles = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(_tokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("correlationId", Guid.NewGuid().ToString())
        };

        if (roles is not null)
        {
            foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                claims.Add(new Claim("roles", role));
            }
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _credentials
        };

        var jwt = _tokenHandler.CreateEncodedJwt(descriptor);
        return new AuthTokenResult(jwt, (int)_tokenLifetime.TotalSeconds, roles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? Array.Empty<string>());
    }

    public object GetJwksDocument() => new { keys = new[] { _publicJsonWebKey } };
}

internal sealed record AuthTokenResult(string AccessToken, int ExpiresIn, IReadOnlyCollection<string> Roles);
