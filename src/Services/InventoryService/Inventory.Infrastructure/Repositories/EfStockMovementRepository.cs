using Inventory.Domain.Entities;
using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Repositories;

public sealed class EfStockMovementRepository : IStockMovementRepository
{
    private readonly InventoryDbContext _context;

    public EfStockMovementRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<StockMovement> AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return movement;
    }

    public async Task<IReadOnlyCollection<StockMovement>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var movements = await _context.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return movements;
    }
}
