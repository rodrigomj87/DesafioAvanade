using Inventory.Domain.Entities;
using Inventory.Domain.Enums;

namespace Inventory.Application.Contracts;

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    StockMovementType Type,
    int Quantity,
    string Reason,
    string? CorrelationId,
    DateTime CreatedAt)
{
    public static StockMovementDto FromEntity(StockMovement movement) =>
        new(
            movement.Id,
            movement.ProductId,
            movement.Type,
            movement.Quantity,
            movement.Reason,
            movement.CorrelationId,
            movement.CreatedAt);
}
