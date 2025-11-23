using Sales.Api.Extensions;
using Sales.Api.Handlers;
using Sales.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSalesLogging();
builder.Services.AddSalesObservability(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSalesInfrastructure(builder.Configuration);
builder.Services.AddRabbitMqPublisher(builder.Configuration);
await builder.Services.AddSalesJwtAuthenticationAsync(builder.Configuration);

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
