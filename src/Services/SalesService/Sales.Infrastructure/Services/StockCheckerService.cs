using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sales.Application.Services;

namespace Sales.Infrastructure.Services;

public sealed class StockCheckerService : IStockChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockCheckerService> _logger;

    public StockCheckerService(HttpClient httpClient, ILogger<StockCheckerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<StockCheckResult> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/inventory/products/{productId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product {ProductId} not found in inventory", productId);
                return new StockCheckResult(productId, false, 0, "Product not found");
            }

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);

            if (product is null)
            {
                _logger.LogError("Failed to deserialize product response for {ProductId}", productId);
                return new StockCheckResult(productId, false, 0, "Failed to retrieve product data");
            }

            var isAvailable = product.Quantity >= quantity;

            _logger.LogInformation(
                "Stock check for {ProductId}: requested {RequestedQuantity}, available {AvailableQuantity}, result: {IsAvailable}",
                productId, quantity, product.Quantity, isAvailable);

            return new StockCheckResult(
                productId,
                isAvailable,
                product.Quantity,
                isAvailable ? null : $"Insufficient stock. Available: {product.Quantity}, Requested: {quantity}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error checking stock for product {ProductId}", productId);
            return new StockCheckResult(productId, false, 0, "Service unavailable");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout checking stock for product {ProductId}", productId);
            return new StockCheckResult(productId, false, 0, "Request timeout");
        }
    }

    public async Task<Dictionary<Guid, StockCheckResult>> CheckMultipleAsync(
        Dictionary<Guid, int> productQuantities,
        CancellationToken cancellationToken = default)
    {
        var tasks = productQuantities.Select(kvp =>
            CheckAvailabilityAsync(kvp.Key, kvp.Value, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(r => r.ProductId, r => r);
    }

    private sealed record ProductDto(Guid Id, string Sku, string Name, int Quantity, string Status);
}
