using Sales.Api.Extensions;
using Sales.Api.Handlers;
using Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSalesLogging();
builder.Services.AddSalesObservability(builder.Configuration);
builder.Services.AddSalesInfrastructure(builder.Configuration);
await builder.Services.AddSalesJwtAuthenticationAsync(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("sales.read", policy => policy.RequireClaim("permissions", "sales.read"));
    options.AddPolicy("sales.write", policy => policy.RequireClaim("permissions", "sales.write"));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    dbContext.Database.Migrate();
}

app.UseExceptionHandler();

app.UseSalesRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sales.Api" }))
    .WithName("HealthCheck");

app.MapOrdersEndpoints();

app.Run();
