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
        
        var httpClient = new HttpClient();
        var jwksJson = await httpClient.GetStringAsync($"{authority}/.well-known/jwks.json");
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
                    IssuerSigningKeys = jwks.GetSigningKeys()
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
