using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public sealed class StockMovement
{
    private StockMovement()
    {
        Reason = string.Empty;
    }

    public StockMovement(Guid id, Guid productId, StockMovementType type, int quantity, string reason, string? correlationId)
    {
        Id = id;
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        Reason = reason;
        CorrelationId = correlationId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string Reason { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
