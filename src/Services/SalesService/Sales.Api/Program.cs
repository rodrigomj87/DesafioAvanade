using Sales.Api.Extensions;
using Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSalesLogging();
builder.Services.AddSalesObservability(builder.Configuration);
builder.Services.AddSalesInfrastructure(builder.Configuration);
await builder.Services.AddSalesJwtAuthenticationAsync(builder.Configuration);

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

app.UseSalesRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Sales.Api" }))
    .WithName("HealthCheck");

app.Run();
