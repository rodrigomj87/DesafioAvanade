using Inventory.Domain.Entities;

namespace Inventory.Domain.Repositories;

public interface IStockMovementRepository
{
    Task<StockMovement> AddAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StockMovement>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
}
