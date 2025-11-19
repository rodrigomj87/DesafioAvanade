using Auth.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/v1/auth/token", (AuthRequest request, TokenService tokenService) =>
{
    var subject = string.IsNullOrWhiteSpace(request.Username) ? "anonymous" : request.Username;
    var requestedRoles = request.Roles?.Length > 0 ? request.Roles : new[] { "inventory.read" };
    var token = tokenService.CreateToken(subject, requestedRoles);

    var response = new AuthResponse(
        token.AccessToken,
        token.ExpiresIn,
        requestedRoles,
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
    );

    return Results.Ok(response);
});

app.MapGet("/.well-known/jwks.json", (TokenService tokenService) => Results.Json(tokenService.GetJwksDocument()));

app.MapGet("/health", () => Results.Ok(new { status = "UP" }));

app.Run();

public record AuthRequest(string Username, string Password, string[]? Roles);

public record AuthResponse(string AccessToken, int ExpiresIn, string[] Roles, string RefreshToken);
