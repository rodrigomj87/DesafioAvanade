using FluentValidation;
using Inventory.Api.Extensions;
using Inventory.Api.Middleware;
using Inventory.Application.Contracts;
using Inventory.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureInventoryLogging();
builder.Services.AddInventoryObservability(builder.Configuration);
builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration);
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

app.UseInventoryRequestLogging();
app.UseExceptionHandler();

app.MapGet("/api/v1/inventory/health", () => Results.Ok(new { status = "UP" }))
    .WithName("GetInventoryHealth");

app.MapGet("/api/v1/inventory/products", async (
    [AsParameters] ProductQueryParameters parameters,
    ProductService service,
    CancellationToken ct) =>
{
    var result = await service.GetPagedAsync(parameters, ct);
    return Results.Ok(result);
})
    .WithName("ListProducts");

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
    .WithName("CreateProduct");

app.Run();
