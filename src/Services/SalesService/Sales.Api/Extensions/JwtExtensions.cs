using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Sales.Api.Extensions;

internal static class JwtExtensions
{
    public static async Task<IServiceCollection> AddSalesJwtAuthenticationAsync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Jwt:Authority"];
        
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);
        string? jwksJson = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                jwksJson = await httpClient.GetStringAsync($"{authority}/.well-known/jwks.json");
                break;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                Console.WriteLine($"⚠️  Tentativa {attempt}/{maxRetries}: Auth Service não disponível, tentando novamente em {delay.TotalSeconds}s...");
                await Task.Delay(delay);
            }
        }

        if (jwksJson == null)
        {
            throw new InvalidOperationException($"Não foi possível obter JWKS do Auth Service após {maxRetries} tentativas. Verifique se o Auth Service está rodando em {authority}");
        }

        var jwks = new JsonWebKeySet(jwksJson);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Audience = configuration["Jwt:Audience"];
                options.RequireHttpsMetadata = configuration.GetValue<bool>("Jwt:RequireHttpsMetadata", true);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.FromMinutes(5),
                    IssuerSigningKeys = jwks.GetSigningKeys(),
                    RoleClaimType = "permissions",
                    NameClaimType = "sub"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers.Append("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
