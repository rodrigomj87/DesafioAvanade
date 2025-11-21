using Inventory.Domain.Enums;

namespace Inventory.Application.Contracts;

public sealed record RegisterStockMovementDto(Guid ProductId, StockMovementType Type, int Quantity, string Reason, string? CorrelationId);
