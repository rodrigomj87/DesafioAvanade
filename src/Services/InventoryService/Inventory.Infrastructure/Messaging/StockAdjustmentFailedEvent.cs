namespace Inventory.Infrastructure.Messaging;

public sealed class StockAdjustmentFailedEvent
{
    public required Guid OrderId { get; init; }
    public required string Reason { get; init; }
    public required List<FailedItemEvent> FailedItems { get; init; }
    public required DateTime FailedAt { get; init; }
}

public sealed class FailedItemEvent
{
    public required Guid ProductId { get; init; }
    public required int RequestedQuantity { get; init; }
    public required int? AvailableQuantity { get; init; }
    public required string FailureReason { get; init; }
}
