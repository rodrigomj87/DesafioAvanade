namespace Inventory.Infrastructure.Messaging;

public sealed class OrderConfirmedEvent
{
    public required Guid OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required List<OrderItemEvent> Items { get; init; }
    public required decimal TotalAmount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class OrderItemEvent
{
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}
