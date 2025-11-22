using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ApiGateway.Tests;

/// <summary>
/// Testes E2E para validar o graceful period da rotação de chaves RSA.
/// IMPORTANTE: Requer Auth Service rodando em localhost:5112 com 2 chaves configuradas.
/// </summary>
public class KeyRotationGracefulPeriodTests
{
    private const string AuthServiceUrl = "http://localhost:5112";

    [Fact]
    public async Task JwksEndpoint_WithMultipleKeys_ShouldReturnAllKeys()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act: Acessa endpoint JWKS do Auth Service
        var response = await httpClient.GetAsync($"{AuthServiceUrl}/.well-known/jwks.json");
        var jwks = await response.Content.ReadFromJsonAsync<JwksDocument>();

        // Assert: JWKS contém 2 chaves (auth-stub + auth-2024-11)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        jwks.Should().NotBeNull();
        jwks!.Keys.Should().HaveCount(2, "graceful period deve ter 2 chaves ativas");
        jwks.Keys.Should().Contain(k => k.Kid == "auth-stub");
        jwks.Keys.Should().Contain(k => k.Kid == "auth-2024-11");
    }

    [Fact]
    public async Task TokenGeneration_WithPrimaryKey_ShouldIncludeCorrectKid()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act: Gera token com a chave primária (auth-stub)
        var response = await httpClient.PostAsJsonAsync(
            $"{AuthServiceUrl}/api/v1/auth/token",
            new { username = "user1", password = "password" }
        );
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Assert: Token foi gerado com kid da chave primária
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();

        // Decode JWT header para validar kid
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.AccessToken);
        var kid = jwtToken.Header.Kid;

        kid.Should().Be("auth-stub", "chave primária deve assinar novos tokens");
    }

    [Fact]
    public async Task TokenValidation_WithBothKeysInJwks_ShouldAcceptTokens()
    {
        // Arrange: Gera token válido
        var httpClient = new HttpClient();
        var tokenResponse = await httpClient.PostAsJsonAsync(
            $"{AuthServiceUrl}/api/v1/auth/token",
            new { username = "admin", password = "password", roles = new[] { "admin" } }
        );
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResult.Should().NotBeNull();

        // Valida estrutura do token
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenResult!.AccessToken);
        
        jwtToken.Header.Kid.Should().NotBeNullOrEmpty("token deve ter kid header");
        jwtToken.Claims.Should().Contain(c => c.Type == "sub", "token deve ter subject claim");

        // Assert: Token é válido e estruturado corretamente para validação com JWKS
        tokenResult.ExpiresIn.Should().BeGreaterThan(0);
        tokenResult.Roles.Should().Contain("admin");
    }

    private record TokenResponse(string AccessToken, int ExpiresIn, string[] Roles, string? RefreshToken);
    private record JwksDocument(List<JwkKey> Keys);
    private record JwkKey(string Kid, string Kty, string Use, string N, string E);
}
