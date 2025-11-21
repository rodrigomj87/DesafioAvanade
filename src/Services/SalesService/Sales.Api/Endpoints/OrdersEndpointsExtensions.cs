using Sales.Application.Contracts;
using Sales.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Builder;

public static class OrdersEndpointsExtensions
{
    public static RouteGroupBuilder MapOrdersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders");

        group.MapPost("/", CreateOrderAsync)
            .WithName("CreateOrder")
            .WithSummary("Create a new order")
            .RequireAuthorization("sales.write");

        group.MapGet("/", GetOrdersAsync)
            .WithName("GetOrders")
            .WithSummary("Get all orders with pagination and filters")
            .RequireAuthorization("sales.read");

        group.MapGet("/{id:guid}", GetOrderByIdAsync)
            .WithName("GetOrderById")
            .WithSummary("Get order by ID")
            .RequireAuthorization("sales.read");

        group.MapPatch("/{id:guid}/status", UpdateOrderStatusAsync)
            .WithName("UpdateOrderStatus")
            .WithSummary("Update order status")
            .RequireAuthorization("sales.write");

        return group;
    }

    private static async Task<Created<OrderResponse>> CreateOrderAsync(
        CreateOrderDto dto,
        IOrderService orderService,
        CancellationToken cancellationToken)
    {
        var response = await orderService.CreateOrderAsync(dto, cancellationToken);
        return TypedResults.Created($"/api/v1/orders/{response.Id}", response);
    }

    private static async Task<Ok<PagedResult<OrderResponse>>> GetOrdersAsync(
        IOrderService orderService,
        int page = 1,
        int pageSize = 20,
        string? customerId = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var response = await orderService.GetOrdersAsync(page, pageSize, customerId, status, fromDate, toDate, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<OrderResponse>, NotFound>> GetOrderByIdAsync(
        Guid id,
        IOrderService orderService,
        CancellationToken cancellationToken)
    {
        var response = await orderService.GetOrderByIdAsync(id, cancellationToken);
        
        if (response == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<OrderResponse>> UpdateOrderStatusAsync(
        Guid id,
        UpdateOrderStatusDto dto,
        IOrderService orderService,
        CancellationToken cancellationToken)
    {
        var response = await orderService.UpdateOrderStatusAsync(id, dto.NewStatus, cancellationToken);
        return TypedResults.Ok(response);
    }
}

public sealed record UpdateOrderStatusDto(string NewStatus);
