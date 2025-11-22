using Inventory.Application.Contracts;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Exceptions;
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
        var existing = await _repository.GetBySkuAsync(request.Sku, cancellationToken);
        if (existing is not null)
        {
            throw new DuplicateSkuException(request.Sku);
        }

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

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQueryParameters parameters, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            parameters.Page,
            parameters.PageSize,
            parameters.Sku,
            parameters.Name,
            (int?)parameters.Status,
            cancellationToken);

        var dtos = items.Select(ProductDto.FromEntity).ToArray();
        return new PagedResult<ProductDto>(dtos, parameters.Page, parameters.PageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<ProductDto>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return products.Select(ProductDto.FromEntity).ToArray();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        return product is not null ? ProductDto.FromEntity(product) : null;
    }
}
