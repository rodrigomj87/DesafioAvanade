using Inventory.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Messaging;

public sealed class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<OrderConfirmedConsumer> _logger;

    public OrderConfirmedConsumer(
        IProductRepository productRepository,
        ILogger<OrderConfirmedConsumer> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var @event = context.Message;
        var correlationId = context.Headers.Get<string>("x-correlation-id") ?? context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Recebido evento OrderConfirmed. OrderId: {OrderId}, CustomerId: {CustomerId}, Items: {ItemCount}, CorrelationId: {CorrelationId}",
            @event.OrderId, @event.CustomerId, @event.Items.Count, correlationId);

        foreach (var item in @event.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, context.CancellationToken);

            if (product is null)
            {
                _logger.LogError(
                    "Produto não encontrado. ProductId: {ProductId}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                    item.ProductId, @event.OrderId, correlationId);
                throw new InvalidOperationException($"Product {item.ProductId} not found");
            }

            if (product.QuantityAvailable < item.Quantity)
            {
                _logger.LogError(
                    "Estoque insuficiente. ProductId: {ProductId}, QuantityRequested: {QuantityRequested}, QuantityAvailable: {QuantityAvailable}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                    item.ProductId, item.Quantity, product.QuantityAvailable, @event.OrderId, correlationId);
                throw new InvalidOperationException($"Insufficient stock for product {item.ProductId}. Requested: {item.Quantity}, Available: {product.QuantityAvailable}");
            }

            product.AdjustQuantity(-item.Quantity);

            _logger.LogInformation(
                "Estoque reduzido. ProductId: {ProductId}, Quantity: {Quantity}, NewQuantityAvailable: {NewQuantityAvailable}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                item.ProductId, item.Quantity, product.QuantityAvailable, @event.OrderId, correlationId);
        }

        _logger.LogInformation(
            "Evento OrderConfirmed processado com sucesso. OrderId: {OrderId}, CorrelationId: {CorrelationId}",
            @event.OrderId, correlationId);
    }
}
