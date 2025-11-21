using Sales.Application.Contracts;

namespace Sales.Application.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderResponse>> GetOrdersAsync(int page, int pageSize, string? customerId, string? status, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderResponse> UpdateOrderStatusAsync(Guid orderId, string newStatus, CancellationToken cancellationToken = default);
}
