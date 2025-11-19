var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var orders = new List<object>();

app.MapGet("/api/v1/sales/health", () => Results.Ok(new { status = "UP" }));

app.MapGet("/api/v1/sales/orders", () => Results.Ok(orders));

app.MapPost("/api/v1/sales/orders", (object order) =>
{
    orders.Add(order);
    return Results.Accepted(value: order);
});

app.Run();
