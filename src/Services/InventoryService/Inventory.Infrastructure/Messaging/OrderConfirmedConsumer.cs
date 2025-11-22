using Inventory.Domain.Entities;
using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Messaging;

public sealed class OrderConfirmedConsumer : IConsumer<OrderConfirmedEvent>
{
    private readonly IProductRepository _productRepository;
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderConfirmedConsumer> _logger;

    public OrderConfirmedConsumer(
        IProductRepository productRepository,
        InventoryDbContext dbContext,
        ILogger<OrderConfirmedConsumer> logger)
    {
        _productRepository = productRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var @event = context.Message;
        var messageId = context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var correlationId = context.Headers.Get<string>("x-correlation-id") ?? context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString();

        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AnyAsync(pm => pm.MessageId == messageId, context.CancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Mensagem já processada (idempotência). MessageId: {MessageId}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                messageId, @event.OrderId, correlationId);
            return;
        }

        _logger.LogInformation(
            "Recebido evento OrderConfirmed. OrderId: {OrderId}, CustomerId: {CustomerId}, Items: {ItemCount}, CorrelationId: {CorrelationId}",
            @event.OrderId, @event.CustomerId, @event.Items.Count, correlationId);

        var failedItems = new List<FailedItemEvent>();

        try
        {
            foreach (var item in @event.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId, context.CancellationToken);

                if (product is null)
                {
                    _logger.LogError(
                        "Produto não encontrado. ProductId: {ProductId}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                        item.ProductId, @event.OrderId, correlationId);
                    
                    failedItems.Add(new FailedItemEvent
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.Quantity,
                        AvailableQuantity = null,
                        FailureReason = "Product not found"
                    });
                    continue;
                }

                if (product.QuantityAvailable < item.Quantity)
                {
                    _logger.LogError(
                        "Estoque insuficiente. ProductId: {ProductId}, QuantityRequested: {QuantityRequested}, QuantityAvailable: {QuantityAvailable}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                        item.ProductId, item.Quantity, product.QuantityAvailable, @event.OrderId, correlationId);
                    
                    failedItems.Add(new FailedItemEvent
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.Quantity,
                        AvailableQuantity = product.QuantityAvailable,
                        FailureReason = "Insufficient stock"
                    });
                    continue;
                }

                product.AdjustQuantity(-item.Quantity);

                _logger.LogInformation(
                    "Estoque reduzido. ProductId: {ProductId}, Quantity: {Quantity}, NewQuantityAvailable: {NewQuantityAvailable}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                    item.ProductId, item.Quantity, product.QuantityAvailable, @event.OrderId, correlationId);
            }

            if (failedItems.Any())
            {
                await PublishCompensationEventAsync(context, @event.OrderId, failedItems, correlationId);
                throw new InvalidOperationException($"Stock adjustment failed for {failedItems.Count} item(s)");
            }

            var processedMessage = new ProcessedMessage(messageId);
            await _dbContext.ProcessedMessages.AddAsync(processedMessage, context.CancellationToken);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "Evento OrderConfirmed processado com sucesso. MessageId: {MessageId}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                messageId, @event.OrderId, correlationId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex,
                "Erro ao processar evento OrderConfirmed. MessageId: {MessageId}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
                messageId, @event.OrderId, correlationId);
            throw;
        }
    }

    private async Task PublishCompensationEventAsync(
        ConsumeContext<OrderConfirmedEvent> context,
        Guid orderId,
        List<FailedItemEvent> failedItems,
        string correlationId)
    {
        var compensationEvent = new StockAdjustmentFailedEvent
        {
            OrderId = orderId,
            Reason = "Stock adjustment failed",
            FailedItems = failedItems,
            FailedAt = DateTime.UtcNow
        };

        await context.Publish(compensationEvent, ctx =>
        {
            ctx.Headers.Set("x-correlation-id", correlationId);
            ctx.Headers.Set("x-source-service", "inventory-service");
        });

        _logger.LogWarning(
            "Publicado evento de compensação StockAdjustmentFailed. OrderId: {OrderId}, FailedItems: {FailedItemsCount}, CorrelationId: {CorrelationId}",
            orderId, failedItems.Count, correlationId);
    }
}
