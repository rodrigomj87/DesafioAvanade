using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api;

public sealed class TokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _primaryCredentials;
    private readonly List<JsonWebKey> _allPublicKeys;
    private readonly TimeSpan _tokenLifetime;
    private readonly string _issuer;
    private readonly string? _audience;

    public TokenService(IOptions<AuthOptions> options)
    {
        var settings = options.Value;
        _issuer = settings.Issuer ?? throw new InvalidOperationException("Auth:Issuer não configurado.");
        _audience = settings.Audience;
        _tokenLifetime = TimeSpan.FromMinutes(settings.TokenLifetimeMinutes > 0 ? settings.TokenLifetimeMinutes : 60);

        if (settings.RsaKeys is null || settings.RsaKeys.Count == 0)
        {
            throw new InvalidOperationException("Auth:RsaKeys não configurado ou vazio. Configure pelo menos uma chave RSA.");
        }

        var primaryKey = settings.RsaKeys.FirstOrDefault(k => k.IsPrimary);
        if (primaryKey is null)
        {
            throw new InvalidOperationException("Nenhuma chave RSA marcada como IsPrimary=true. Defina uma chave primária.");
        }

        _allPublicKeys = new List<JsonWebKey>();
        SigningCredentials? primaryCreds = null;

        foreach (var keyConfig in settings.RsaKeys)
        {
            if (string.IsNullOrWhiteSpace(keyConfig.KeyId))
            {
                throw new InvalidOperationException("KeyId não pode ser vazio em Auth:RsaKeys.");
            }

            if (string.IsNullOrWhiteSpace(keyConfig.RsaKeyXml))
            {
                throw new InvalidOperationException($"RsaKeyXml não pode ser vazio para KeyId={keyConfig.KeyId}.");
            }

            var rsa = RSA.Create(2048);
            rsa.FromXmlString(keyConfig.RsaKeyXml);

            var parameters = rsa.ExportParameters(true);

            if (keyConfig.IsPrimary)
            {
                var signingKey = new RsaSecurityKey(parameters)
                {
                    KeyId = keyConfig.KeyId
                };
                primaryCreds = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
            }

            var publicParameters = new RSAParameters
            {
                Modulus = parameters.Modulus,
                Exponent = parameters.Exponent
            };

            var publicKey = new RsaSecurityKey(publicParameters)
            {
                KeyId = keyConfig.KeyId
            };

            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
            _allPublicKeys.Add(jwk);

            rsa.Dispose();
        }

        _primaryCredentials = primaryCreds ?? throw new InvalidOperationException("Falha ao configurar SigningCredentials para chave primária.");
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
            SigningCredentials = _primaryCredentials
        };

        var jwt = _tokenHandler.CreateEncodedJwt(descriptor);
        return new AuthTokenResult(jwt, (int)_tokenLifetime.TotalSeconds, roles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? Array.Empty<string>());
    }

    public object GetJwksDocument() => new { keys = _allPublicKeys };
}

public sealed record AuthTokenResult(string AccessToken, int ExpiresIn, IReadOnlyCollection<string> Roles);
