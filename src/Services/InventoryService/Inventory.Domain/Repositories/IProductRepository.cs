namespace Inventory.Domain.Repositories;

using Inventory.Domain.Entities;

public interface IProductRepository
{
    Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? sku = null,
        string? name = null,
        int? status = null,
        CancellationToken cancellationToken = default);
}
