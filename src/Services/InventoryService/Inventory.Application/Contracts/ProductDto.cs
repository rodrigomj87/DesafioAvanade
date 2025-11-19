using Inventory.Domain.Entities;

namespace Inventory.Application.Contracts;

public sealed record ProductDto(Guid Id, string Sku, string Name, string Description, decimal Price, int QuantityAvailable)
{
    public static ProductDto FromEntity(Product product) =>
        new(product.Id, product.Sku, product.Name, product.Description, product.Price, product.QuantityAvailable);
}
