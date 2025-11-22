using Inventory.Application.Contracts;
using Inventory.Domain.Entities;
using Inventory.Domain.Repositories;

namespace Inventory.Application.Services;

public sealed class StockMovementService
{
    private readonly IStockMovementRepository _repository;
    private readonly IProductRepository _productRepository;

    public StockMovementService(IStockMovementRepository repository, IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
    }

    public async Task<StockMovementDto> RegisterAsync(RegisterStockMovementDto request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantidade deve ser positiva.");
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException($"Produto {request.ProductId} não encontrado.");
        }

        var movement = new StockMovement(
            Guid.NewGuid(),
            request.ProductId,
            request.Type,
            request.Quantity,
            request.Reason,
            request.CorrelationId);

        var delta = request.Type == Domain.Enums.StockMovementType.In ? request.Quantity : -request.Quantity;
        product.AdjustQuantity(delta);
        await _productRepository.UpdateAsync(product, cancellationToken);

        var stored = await _repository.AddAsync(movement, cancellationToken);
        return StockMovementDto.FromEntity(stored);
    }

    public async Task<IReadOnlyCollection<StockMovementDto>> GetHistoryAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return Array.Empty<StockMovementDto>();
        }

        var history = await _repository.GetByProductAsync(productId, cancellationToken);
        return history.Select(StockMovementDto.FromEntity).ToArray();
    }
}
