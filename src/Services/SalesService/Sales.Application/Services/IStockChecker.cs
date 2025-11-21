namespace Sales.Application.Services;

public interface IStockChecker
{
    Task<StockCheckResult> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, StockCheckResult>> CheckMultipleAsync(Dictionary<Guid, int> productQuantities, CancellationToken cancellationToken = default);
}

public sealed record StockCheckResult(
    Guid ProductId,
    bool IsAvailable,
    int AvailableQuantity,
    string? ErrorMessage = null
);
