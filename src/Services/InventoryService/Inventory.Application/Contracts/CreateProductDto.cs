namespace Inventory.Application.Contracts;

public sealed record CreateProductDto(string Sku, string Name, string Description, decimal Price, int QuantityAvailable);
