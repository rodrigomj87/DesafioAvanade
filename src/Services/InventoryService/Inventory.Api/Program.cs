using Inventory.Application.Contracts;
using Inventory.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInventoryApplication();
builder.Services.AddInventoryInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/v1/inventory/health", () => Results.Ok(new { status = "UP" }));

app.MapGet("/api/v1/inventory/products", async (ProductService service, CancellationToken ct) =>
{
    var products = await service.ListAsync(ct);
    return Results.Ok(products);
});

app.MapPost("/api/v1/inventory/products", async (CreateProductDto request, ProductService service, CancellationToken ct) =>
{
    var created = await service.CreateAsync(request, ct);
    return Results.Created($"/api/v1/inventory/products/{created.Id}", created);
});

app.Run();
