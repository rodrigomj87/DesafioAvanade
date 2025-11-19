using Inventory.Application.Contracts;
using Inventory.Domain.Entities;
using Inventory.Domain.Repositories;

namespace Inventory.Application.Services;

public sealed class ProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto request, CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.Sku, request.Name, request.Description, request.Price, request.QuantityAvailable);
        var stored = await _repository.AddAsync(product, cancellationToken);
        return ProductDto.FromEntity(stored);
    }

    public async Task<IReadOnlyCollection<ProductDto>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return products.Select(ProductDto.FromEntity).ToArray();
    }
}
