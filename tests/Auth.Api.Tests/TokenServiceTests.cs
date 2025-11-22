using Auth.Api;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace Auth.Api.Tests;

public class TokenServiceTests : IDisposable
{
    private readonly AuthOptions _authOptions;

    public TokenServiceTests()
    {
        _authOptions = new AuthOptions
        {
            Issuer = "https://auth.local",
            Audience = "test-audience",
            TokenLifetimeMinutes = 60,
            RsaKeys = new List<RsaKeyConfig>
            {
                new()
                {
                    KeyId = "auth-test-1",
                    RsaKeyXml = "<RSAKeyValue><Modulus>luHEb58gQva+YXuHrX15WKPAlkAsy8IHtXgGz3Gbj34qtwhkiwJLlVqyqK3oh9Gc9QUlmXsjl/8iWRKsBrh7el962z2zkKZpAa85hnDmomc2Xxq2cg8WbvSy/95EE7uTEKFsFdYMNnMbTVHn3bSWhgvR/OqPq9NH2lOQfqX86PBGa8cFk6tZ88rQPKq7qhDDTtiS15yBRXwamNQluEF0XYOAXXp8CBGwpYH9uG1drBT0fGQxcN7syDIt0KQYVw6oMkhZcGVU/R3jBzq6vDlYqx4yPwqg6vuex65LycGkJSajEdy8ATZO19MlYUiOcaHrUVdl2vrF/p6fisyyIlDFSQ==</Modulus><Exponent>AQAB</Exponent><P>yHCU9yjrqY7c052bZ/l+Rbi8pqrZBejkyz9XBNWOwHwOLOY5zW/khdlhdsXZyBREPNtOJtND7TOZgEJVkSJoPVzF6+Urte+yrgcy2mdY4Yiuj5brtH16trr9UcIBvGWgQZqjC0684ZWr9Q9FZv98mInR3oWHvRUTeb+B6Eed9V8=</P><Q>wLSBs811WpImCjCRC91Tpo/iFndEvEns/9Ondec1U4LXr5orVx7rbMI6kPsq+hqn72I7rsjtogqbh/BfQzVvhBAGmI7P9KtalFqFRHh6WDE/ucrDoRYLGNidiPKrM07rvwjt1wfDstL6PePUQDq7n5lJORDA/snDytfAHxNm3lc=</Q><DP>vHW+WrXK/t4om3/xAs3/ACc2ma7B52CW3tNrSkxRsIH0Ahtl9ddjEsbVcQuOPSVxE5dP4Al8MKb3iTTVp2SmhiuYnLgZZTM+2YpDMj9Bvqz5FGFetZHVs8IVNK4pW65iA80VyCf8SE6mJAB7zOAjknjjL0p12j7a1PpPdsr/phk=</DP><DQ>d52UBO+7sISXaCvNXPf9PJ6DUD6oQ9f/bK0MfXsO9s944l9oF6PxrojBoczGWIjDRSxMr6m5F6RN9ouWFjnNBsSL05aSLuVOTFgD8QR3rNalH8ZkuFDegoWzA5bd/fM3ElWQjLEdIJslCdWpolTSsbaMZQepRH4pMnfJoYCBkfE=</DQ><InverseQ>NaLj/DLRIb+VpvQWv2xyFu5SQGDHX++iU8x8Ph9HDpCcBdo1QHQ1hklloIOO6DeFvTVAAh5Nf+VNV5lkIKdB1ndqlN89n94wPGcrZqEaoQdyu/LD3GzkCQb5jIAzQxeBoXnH46Hc0q4c4oWmBnur8+qXFPbT1WQy+myJMK8Foho=</InverseQ><D>NTYwOhGLpjIxGF4hjo0G47z0qci+Q6oNX/GRrU1qyGN9mwmJtAC+OVFAWv7iWCwTx9Hl3cgw2GUxbuqH4uLjIVhVEsci8evOcrqidgiRPktaQJZCK92oUr9X9ackqTTo+9+CJ/1SV02nHlC5jlYnE+VNTH5I5xdfat83wYK2vEueUzTU8E4MEIdVt6Qvno+188sn1Jd7uZB49UFSyNkByLyd0tW5ANmvtPv2EChDVhOw84G9CzeNGqI9xwJgH7z0DEfM6Kw3W9hzX4u9fjBBz1vPK2oMGy9AQSRuKDoiHOhcSrvTa0ImGxll8nf2MXPErpS0IcQMkAKVz/hinGDrNQ==</D></RSAKeyValue>",
                    IsPrimary = true
                },
                new()
                {
                    KeyId = "auth-test-2",
                    RsaKeyXml = "<RSAKeyValue><Modulus>m+LOpS55UY8SWeA7rsrcWu8a0glJLF6Ie6HQYRhQNwoFA5NHVBFjZH7NZyB77yEIVbx0KNgdG4UAx0xspigR866Fn3KDQEAkZNdNzGSR6V0iC+Rwl5lJSQu8MCX4xuN6xuCn3PQB7ghkPTctxHTtkIz8rbnTABqogwIyFINzAGmPV1yIqP3/5jK0PDNC/uarq6StOUqK3aby4rpPx80E9iHS1nZBMiKqJiJ6V6VaUBTaOWZMdzYaR1Zf3JxTiMCIp+CrtQBG0kA7ZgMJiuK1Pp22b/6AQE/vM9znQWTgnZMFTSqa1Yq9uDkXgtz9VFbfIlwN7DP1/bdDGztnepFhcQ==</Modulus><Exponent>AQAB</Exponent><P>ypBUwBck5OrzL/E8hOG8ZpPvJD9OkbhLTTSnZBcgzmtctQlE7FooyComxzfCeD5dB/EZG6aIsXzZDxx7cpdVJeO+tR6kmNAkgEMkujjx4PJ3SbeHPaGI4bfTal3+PiJIncMZaud6FadpeFpLUFtk096mKSRkGhjBhhwKQsosQWM=</P><Q>xQIy7j0z38Rd10CocifDyQNjZExF+1cZMiclkttTr/vIh3eHluSpH/u9R5F8pReIYYYo1hC4W/SwI2VECoLeuzI/ExU2yY88vJLVFA/C6LuuyvtLw0DukvjEv1fqdIto13UfRHrmWKHANaTEW0wD1FDI+Q//mx61hqW8FKdmVBs=</Q><DP>rC/mKudBkGn70MwScq5kdRnZVYmO4yWytO1GQTm7xFvVEXrQoMy/D+9syTmn0IjKkf6rJir41U7YQBUpBq6HTc77hICIz/FWpcTtinLvgRT6u6oLhyMx9RVK3IcLoIuKCLkYeRxNVezaJdkzXhpiagtAWYa6pMhYcQSS4FV1Kxs=</DP><DQ>JtgqJqNWqRZU0m5jSC7jAa1zWYFYgxhHVYY2eGBafeaNQXil+XCYkD/YsFXVV8/uChyjzKF7WFZErJX49oGbXRmpKiASr2dYuG3TKaN58N2gt1EZPSR+Zeg7EudIyTX7tA/Z3GzHIk6yUWFtffrc/m33UnR/F5zUefoPvZrMKGU=</DQ><InverseQ>M7bwU7vl2M4dbRCbjYWPNd19AHezLaj0jQ5q3440X0lpgMh8rEV9E+oBvX76fbdgXUAhw7fLcF1TV3ErNL6lxT2PRCtPxE5Fiif5gOzMRGgms5nMLHZYVM0tc/K7m1lUjXJZNRt10WJybTM1foECY7wCDI9WYUBEDX/qOC6vxy0=</InverseQ><D>fEZBxfAZ6cpsr9gS3JN/PsN2F1It0sB+gEmNTYrLkEVdn+8PI1dFbL2mVPvRsd8cvPUjidhYxDKxXzFvQLubLDDK+EWbm2JHYVk//Z2DsY588QwqMsGSajh/y8bJ4Q1DOT+qs3yX+UMZXrAZvZAWQHIVkw4cx+14wBDFChONYBqJDks+z9J2NZeOkzO+XKJacvzVnuMCtyfE4CFh+5u6kk2OMDbFZy/PqdAZgQnCvRWLCEthSb07gUs0SEbccH+Tq7s88h0OPwHF4eBqLKVdMewdtijyPkexy+rGTWgvWxjw+7Gtqj9l2j1oOho6benvRpVYik7tBN/2NDUeFn3C7Q==</D></RSAKeyValue>",
                    IsPrimary = false
                }
            }
        };
    }

    [Fact]
    public void CreateToken_WithMultipleKeys_ShouldUsePrivateKey()
    {
        var options = Options.Create(_authOptions);
        var tokenService = new TokenService(options);

        var result = tokenService.CreateToken("test-user", new[] { "admin" });

        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().Be(3600);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        token.Header["kid"].Should().Be("auth-test-1");
        token.Header["alg"].Should().Be("RS256");
    }

    [Fact]
    public void GetJwksDocument_WithMultipleKeys_ShouldReturnAllPublicKeys()
    {
        var options = Options.Create(_authOptions);
        var tokenService = new TokenService(options);

        var jwks = tokenService.GetJwksDocument();

        jwks.Should().NotBeNull();
        var json = JsonSerializer.Serialize(jwks);
        var doc = JsonSerializer.Deserialize<JsonDocument>(json);

        var keysArray = doc!.RootElement.GetProperty("keys");
        keysArray.GetArrayLength().Should().Be(2);

        var key1 = keysArray[0];
        key1.GetProperty("kid").GetString().Should().Be("auth-test-1");
        key1.GetProperty("kty").GetString().Should().Be("RSA");

        var key2 = keysArray[1];
        key2.GetProperty("kid").GetString().Should().Be("auth-test-2");
        key2.GetProperty("kty").GetString().Should().Be("RSA");
    }

    [Fact]
    public void Constructor_WithoutPrimaryKey_ShouldThrowException()
    {
        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth.local",
            Audience = "test-audience",
            RsaKeys = new List<RsaKeyConfig>
            {
                new()
                {
                    KeyId = "auth-test-1",
                    RsaKeyXml = "<RSAKeyValue><Modulus>test</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>",
                    IsPrimary = false
                }
            }
        });

        var act = () => new TokenService(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Nenhuma chave RSA marcada como IsPrimary=true. Defina uma chave primária.");
    }

    [Fact]
    public void Constructor_WithEmptyRsaKeys_ShouldThrowException()
    {
        var options = Options.Create(new AuthOptions
        {
            Issuer = "https://auth.local",
            Audience = "test-audience",
            RsaKeys = new List<RsaKeyConfig>()
        });

        var act = () => new TokenService(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Auth:RsaKeys não configurado ou vazio. Configure pelo menos uma chave RSA.");
    }

    public void Dispose()
    {
    }
}
