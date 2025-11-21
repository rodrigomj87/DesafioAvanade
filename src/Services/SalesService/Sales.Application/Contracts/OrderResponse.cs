namespace Sales.Application.Contracts;

public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<OrderItemResponse> Items
);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);
