using FluentValidation;
using Inventory.Api.Extensions;
using Inventory.Api.Middleware;
using Inventory.Application.Contracts;
using Inventory.Application.Services;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var builder = WebApplication.CreateBuilder(args);

builder.ConfigureInventoryLogging();
builder.Services.AddInventoryObservability(builder.Configuration);
builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration);

await builder.Services.AddInventoryJwtAuthenticationAsync(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    dbContext.Database.Migrate();
}

app.UseInventoryRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/v1/inventory/health", () => Results.Ok(new { status = "UP" }))
    .WithName("GetInventoryHealth")
    .AllowAnonymous();

app.MapGet("/api/v1/inventory/products", async (
    [AsParameters] ProductQueryParameters parameters,
    ProductService service,
    CancellationToken ct) =>
{
    var result = await service.GetPagedAsync(parameters, ct);
    return Results.Ok(result);
})
    .WithName("ListProducts")
    .RequireAuthorization();

app.MapGet("/api/v1/inventory/products/{id:guid}", async (
    Guid id,
    ProductService service,
    CancellationToken ct) =>
{
    var product = await service.GetByIdAsync(id, ct);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
    .WithName("GetProductById")
    .RequireAuthorization();

app.MapPost("/api/v1/inventory/products", async (
    CreateProductDto request,
    ProductService service,
    IValidator<CreateProductDto> validator,
    CancellationToken ct) =>
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
    {
        throw new ValidationException(validationResult.Errors);
    }

    var created = await service.CreateAsync(request, ct);
    return Results.Created($"/api/v1/inventory/products/{created.Id}", created);
})
    .WithName("CreateProduct")
    .RequireAuthorization();

app.Run();
