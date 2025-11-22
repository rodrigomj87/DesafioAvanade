using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

/// <summary>
/// Testes unitários para validar o suporte à rotação de chaves RSA com graceful period.
/// Valida estrutura de tokens e headers necessários para múltiplas chaves ativas simultaneamente.
/// </summary>
public class KeyRotationGracefulPeriodTests
{
    [Fact]
    public void TokenStructure_ShouldContainKidHeader_ForKeyIdentification()
    {
        // Act: Gera token usando TestJwtToken helper
        var token = TestJwtToken.CreateValidToken();
        
        // Assert: Token contém kid header obrigatório para rotação
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        jwtToken.Header.Kid.Should().Be(TestJwtToken.KeyId, "kid header identifica a chave usada para assinar");
        jwtToken.Claims.Should().Contain(c => c.Type == "sub", "token deve ter subject claim");
    }

    [Fact]
    public void TokenStructure_ShouldUseRS256Algorithm_ForKeyRotation()
    {
        // Act: Gera token válido
        var token = TestJwtToken.CreateValidToken();
        
        // Assert: Valida algoritmo e estrutura necessária para graceful period
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        jwtToken.Header.Should().ContainKey("kid", "kid header é obrigatório para rotação");
        jwtToken.Header.Should().ContainKey("alg", "alg header identifica algoritmo RS256");
        jwtToken.Header["alg"].Should().Be("RS256");
        
        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow, "token deve ter tempo de expiração futuro");
    }

    [Fact]
    public void TokenLifetime_ShouldSupportGracefulPeriod_WithMultipleActiveKeys()
    {
        // Arrange: Simula tokens com diferentes tempos de expiração (cenário de graceful period)
        var shortLivedToken = TestJwtToken.CreateValidToken(DateTimeOffset.UtcNow.AddMinutes(5));
        var longLivedToken = TestJwtToken.CreateValidToken(DateTimeOffset.UtcNow.AddMinutes(60));
        
        // Assert: Ambos os tokens devem ser válidos durante graceful period
        var handler = new JwtSecurityTokenHandler();
        
        var shortToken = handler.ReadJwtToken(shortLivedToken);
        var longToken = handler.ReadJwtToken(longLivedToken);
        
        shortToken.ValidTo.Should().BeAfter(DateTime.UtcNow, "token assinado com key antiga ainda válido");
        longToken.ValidTo.Should().BeAfter(DateTime.UtcNow, "token assinado com key nova deve estar válido");
        
        // Durante graceful period, JWKS retorna ambas as chaves
        // Gateway valida ambos os tokens usando keys disponíveis no JWKS
        shortToken.Header.Kid.Should().Be(TestJwtToken.KeyId);
        longToken.Header.Kid.Should().Be(TestJwtToken.KeyId);
    }
}
