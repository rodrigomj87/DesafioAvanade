using Inventory.Domain.Entities;
using Inventory.Domain.Repositories;

namespace Inventory.Infrastructure.Repositories;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly List<Product> _products = new();

    public Task<Product> AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products.Add(product);
        return Task.FromResult(product);
    }

    public Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult((IReadOnlyCollection<Product>)_products.AsReadOnly());
}
