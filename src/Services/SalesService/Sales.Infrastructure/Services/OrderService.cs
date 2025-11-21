using Sales.Application.Contracts;
using Sales.Application.Services;
using Sales.Domain.Entities;
using Sales.Domain.Repositories;
using Sales.Domain.ValueObjects;
using Sales.Domain.Enums;
using Microsoft.Extensions.Logging;
using FluentValidation;

namespace Sales.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStockChecker _stockChecker;
    private readonly IValidator<CreateOrderDto> _validator;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IStockChecker stockChecker,
        IValidator<CreateOrderDto> validator,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _stockChecker = stockChecker;
        _validator = validator;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var stockChecks = new Dictionary<Guid, int>();
        foreach (var item in dto.Items)
        {
            if (stockChecks.ContainsKey(item.ProductId))
            {
                stockChecks[item.ProductId] += item.Quantity;
            }
            else
            {
                stockChecks[item.ProductId] = item.Quantity;
            }
        }

        var stockResults = await _stockChecker.CheckMultipleAsync(stockChecks, cancellationToken);

        var unavailableProducts = stockResults
            .Where(r => !r.Value.IsAvailable)
            .Select(r => r.Key)
            .ToList();

        if (unavailableProducts.Any())
        {
            _logger.LogWarning("Stock unavailable for products: {ProductIds}", string.Join(", ", unavailableProducts));
            throw new InvalidOperationException($"Insufficient stock for products: {string.Join(", ", unavailableProducts)}");
        }

        var orderItems = dto.Items.Select(item =>
            OrderItem.Create(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                Money.Create(item.UnitPrice, "BRL")
            )
        ).ToList();

        var customerId = CustomerId.Create(dto.CustomerId);
        var order = Order.Create(customerId, orderItems);

        await _orderRepository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId} with {ItemCount} items",
            order.Id, dto.CustomerId, orderItems.Count);

        return MapToOrderResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(
        int page,
        int pageSize,
        string? customerId,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        OrderStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
        {
            statusEnum = parsedStatus;
        }

        var (orders, totalCount) = await _orderRepository.GetPagedAsync(
            page,
            pageSize,
            customerId,
            statusEnum,
            fromDate,
            toDate,
            cancellationToken);

        var orderResponses = orders.Select(MapToOrderResponse).ToList();

        return PagedResult<OrderResponse>.Create(orderResponses, page, pageSize, totalCount);
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        return order != null ? MapToOrderResponse(order) : null;
    }

    public async Task<OrderResponse> UpdateOrderStatusAsync(Guid orderId, string newStatus, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var statusEnum))
        {
            throw new ArgumentException($"Invalid status: {newStatus}");
        }

        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new KeyNotFoundException($"Order {orderId} not found");
        }

        switch (statusEnum)
        {
            case OrderStatus.Confirmed:
                order.ConfirmOrder();
                break;
            case OrderStatus.Cancelled:
                order.CancelOrder();
                break;
            case OrderStatus.Failed:
                order.MarkAsFailed();
                break;
            default:
                throw new InvalidOperationException($"Cannot transition to status {newStatus}");
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, newStatus);

        return MapToOrderResponse(order);
    }

    private static OrderResponse MapToOrderResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(item => new OrderItemResponse(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice
            )).ToList()
        );
    }
}
