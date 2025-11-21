namespace Sales.Application.Contracts;

public sealed record CreateOrderDto(
    string CustomerId,
    List<OrderItemDto> Items
);
