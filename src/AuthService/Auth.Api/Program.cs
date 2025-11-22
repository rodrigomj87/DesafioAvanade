using Auth.Api;
using Auth.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AuthDatabase")
    ?? throw new InvalidOperationException("Connection string 'AuthDatabase' not found.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<Auth.Api.Services.RefreshTokenService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/v1/auth/token", async (AuthRequest request, TokenService tokenService, Auth.Api.Services.RefreshTokenService refreshTokenService) =>
{
    var subject = string.IsNullOrWhiteSpace(request.Username) ? "anonymous" : request.Username;
    var requestedRoles = request.Roles?.Length > 0 ? request.Roles : new[] { "inventory.read" };
    var token = tokenService.CreateToken(subject, requestedRoles);

    var refreshToken = await refreshTokenService.CreateRefreshTokenAsync(subject, request.DeviceId);

    var response = new AuthResponse(
        token.AccessToken,
        token.ExpiresIn,
        requestedRoles,
        refreshToken.Token
    );

    return Results.Ok(response);
});

app.MapGet("/.well-known/jwks.json", (TokenService tokenService) => Results.Json(tokenService.GetJwksDocument()));

app.MapPost("/api/v1/auth/refresh", async (RefreshRequest request, Auth.Api.Services.RefreshTokenService refreshTokenService) =>
{
    try
    {
        var accessToken = await refreshTokenService.RefreshAccessTokenAsync(request.RefreshToken);
        return Results.Ok(new RefreshResponse(accessToken, 3600));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/v1/auth/revoke", async (RevokeRequest request, Auth.Api.Services.RefreshTokenService refreshTokenService) =>
{
    try
    {
        await refreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        return Results.Ok(new { message = "Token revoked successfully" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "UP" }));

app.Run();

public record AuthRequest(string Username, string Password, string[]? Roles, string? DeviceId);

public record AuthResponse(string AccessToken, int ExpiresIn, string[] Roles, string RefreshToken);

public record RefreshRequest(string RefreshToken);

public record RefreshResponse(string AccessToken, int ExpiresIn);

public record RevokeRequest(string RefreshToken);
