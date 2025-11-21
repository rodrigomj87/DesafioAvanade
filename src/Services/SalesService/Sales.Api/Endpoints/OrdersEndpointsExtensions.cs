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
            .RequireAuthorization();

        group.MapGet("/", GetOrdersAsync)
            .WithName("GetOrders")
            .WithSummary("Get all orders with pagination and filters")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetOrderByIdAsync)
            .WithName("GetOrderById")
            .WithSummary("Get order by ID")
            .RequireAuthorization();

        group.MapPatch("/{id:guid}/status", UpdateOrderStatusAsync)
            .WithName("UpdateOrderStatus")
            .WithSummary("Update order status")
            .RequireAuthorization();

        return group;
    }

    private static async Task<Results<Created<OrderResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> CreateOrderAsync(
        CreateOrderDto dto,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await orderService.CreateOrderAsync(dto, cancellationToken);
            return TypedResults.Created($"/api/v1/orders/{response.Id}", response);
        }
        catch (FluentValidation.ValidationException ex)
        {
            logger.LogWarning("Validation failed: {Errors}", string.Join(", ", ex.Errors.Select(e => e.ErrorMessage)));
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)),
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
        {
            logger.LogWarning("Stock unavailable: {Message}", ex.Message);
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Stock Unavailable",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
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

    private static async Task<Results<Ok<OrderResponse>, NotFound<ProblemDetails>>> GetOrderByIdAsync(
        Guid id,
        IOrderService orderService,
        CancellationToken cancellationToken)
    {
        var response = await orderService.GetOrderByIdAsync(id, cancellationToken);
        
        if (response == null)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Order Not Found",
                Detail = $"Order with ID {id} was not found",
                Status = StatusCodes.Status404NotFound
            });
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<OrderResponse>, NotFound<ProblemDetails>, BadRequest<ProblemDetails>>> UpdateOrderStatusAsync(
        Guid id,
        UpdateOrderStatusDto dto,
        IOrderService orderService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await orderService.UpdateOrderStatusAsync(id, dto.NewStatus, cancellationToken);
            return TypedResults.Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Order not found: {Message}", ex.Message);
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Order Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid status: {Message}", ex.Message);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Status",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Invalid transition: {Message}", ex.Message);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid Transition",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}

public sealed record UpdateOrderStatusDto(string NewStatus);
