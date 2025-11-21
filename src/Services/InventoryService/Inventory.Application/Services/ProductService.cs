using Inventory.Application.Contracts;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Repositories;

namespace Inventory.Application.Services;

public sealed class ProductService
{
    private readonly IProductRepository _repository;
    private readonly IStockMovementRepository _stockMovementRepository;

    public ProductService(IProductRepository repository, IStockMovementRepository stockMovementRepository)
    {
        _repository = repository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto request, CancellationToken cancellationToken)
    {
        var product = new Product(Guid.NewGuid(), request.Sku, request.Name, request.Description, request.Price, request.QuantityAvailable);
        var stored = await _repository.AddAsync(product, cancellationToken);

        if (stored.QuantityAvailable > 0)
        {
            var initialMovement = new StockMovement(
                Guid.NewGuid(),
                stored.Id,
                StockMovementType.In,
                stored.QuantityAvailable,
                "Cadastro inicial do produto",
                null);

            await _stockMovementRepository.AddAsync(initialMovement, cancellationToken);
        }

        return ProductDto.FromEntity(stored);
    }

    public async Task<IReadOnlyCollection<ProductDto>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return products.Select(ProductDto.FromEntity).ToArray();
    }
}
