using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Text.Json;
using Inventory.Application.Services;
using Inventory.Domain.Enums;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Inventory.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace Inventory.Infrastructure.Messaging;

public sealed class RabbitMqConsumer : BackgroundService
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly RabbitMqConsumerSettings _settings;
    private readonly InventoryMetrics _metrics;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumer(
        IServiceProvider serviceProvider,
        ILogger<RabbitMqConsumer> logger,
        RabbitMqConsumerSettings settings,
        InventoryMetrics metrics)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings.Host,
                    Port = _settings.Port,
                    UserName = _settings.Username,
                    Password = _settings.Password,
                    DispatchConsumersAsync = true
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare(
                    exchange: _settings.ExchangeName,
                    type: _settings.ExchangeType,
                    durable: true,
                    autoDelete: false);

                _channel.QueueDeclare(
                    queue: _settings.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _channel.QueueBind(
                    queue: _settings.QueueName,
                    exchange: _settings.ExchangeName,
                    routingKey: _settings.RoutingKey);

                _logger.LogInformation(
                    "RabbitMQ consumer conectado. Queue: {Queue}, Exchange: {Exchange}, RoutingKey: {RoutingKey}",
                    _settings.QueueName,
                    _settings.ExchangeName,
                    _settings.RoutingKey);

                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "[ERRO] Tentativa {Attempt}/{MaxRetries}: Erro ao conectar RabbitMQ consumer, tentando novamente em {Delay}s...",
                    attempt,
                    maxRetries,
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }

        if (_channel == null)
        {
            _logger.LogError("[ERRO] Falha ao conectar RabbitMQ consumer após {MaxRetries} tentativas", maxRetries);
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (sender, ea) =>
        {
            var propagationContext = ExtractPropagationContext(ea.BasicProperties);
            var correlationId = GetHeaderValue(ea.BasicProperties, "x-correlation-id") ?? Guid.NewGuid().ToString();

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {

            try
            {
                var headersDict = ea.BasicProperties?.Headers?.ToDictionary(kv => kv.Key, kv =>
                    kv.Value switch
                    {
                        byte[] b => Encoding.UTF8.GetString(b),
                        string s => s,
                        _ => kv.Value?.ToString() ?? string.Empty
                    }) ?? new Dictionary<string, string>();

                _logger.LogInformation("[DEBUG] RabbitMQ received headers: {Headers}", System.Text.Json.JsonSerializer.Serialize(headersDict));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WARN] Falha ao serializar headers do RabbitMQ para debug");
            }
            Activity? activity = null;
            OrderConfirmedEvent? orderEvent = null;
            try
            {
                activity = InventoryTelemetry.ActivitySource.StartActivity(
                    "RabbitMQ Consume",
                    ActivityKind.Consumer,
                    propagationContext.ActivityContext);

                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination", _settings.QueueName);
                activity?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);
                activity?.SetTag("messaging.message_id", ea.BasicProperties?.MessageId);
                activity?.SetTag("messaging.operation", "process");
                activity?.SetTag("messaging.conversation_id", correlationId);

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                orderEvent = JsonSerializer.Deserialize<OrderConfirmedEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (orderEvent == null)
                {
                    _logger.LogWarning("Mensagem deserializada como null. RawMessage: {Message}", message);
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }

                _logger.LogInformation(
                    "[INFO] Evento OrderConfirmed recebido. OrderId: {OrderId}, CustomerId: {CustomerId}, Items: {ItemCount}, CorrelationId: {CorrelationId}",
                    orderEvent.OrderId,
                    orderEvent.CustomerId,
                    orderEvent.Items.Count,
                    correlationId);

                using var scope = _serviceProvider.CreateScope();
                var stockMovementService = scope.ServiceProvider.GetRequiredService<StockMovementService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var productRepository = scope.ServiceProvider.GetRequiredService<Inventory.Domain.Repositories.IProductRepository>();

                var messageId = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();

                // idempotency: skip if already processed
                var alreadyProcessed = await dbContext.ProcessedMessages
                    .AnyAsync(pm => pm.MessageId == messageId, stoppingToken);

                if (alreadyProcessed)
                {
                    _logger.LogInformation("Mensagem já processada (idempotência). MessageId: {MessageId}", messageId);
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var failedItems = new List<FailedItemEvent>();

                foreach (var item in orderEvent.Items)
                {
                    try
                    {
                        var product = await productRepository.GetByIdAsync(item.ProductId, stoppingToken);
                        if (product is null)
                        {
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
                            failedItems.Add(new FailedItemEvent
                            {
                                ProductId = item.ProductId,
                                RequestedQuantity = item.Quantity,
                                AvailableQuantity = product.QuantityAvailable,
                                FailureReason = "Insufficient stock"
                            });
                            continue;
                        }

                        await stockMovementService.RegisterAsync(new Inventory.Application.Contracts.RegisterStockMovementDto(
                            item.ProductId,
                            StockMovementType.Out,
                            item.Quantity,
                            $"Venda - Pedido {orderEvent.OrderId}",
                            orderEvent.OrderId.ToString()
                        ), stoppingToken);

                        _metrics.TrackStockUpdate(item.ProductId, orderEvent.OrderId, item.Quantity);

                        _logger.LogInformation(
                            "[INFO] Baixa de estoque registrada. ProductId: {ProductId}, Quantity: {Quantity}, OrderId: {OrderId}, CustomerId: {CustomerId}, CorrelationId: {CorrelationId}",
                            item.ProductId,
                            item.Quantity,
                            orderEvent.OrderId,
                            orderEvent.CustomerId,
                            correlationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "[ERRO] Erro ao processar item do pedido. ProductId: {ProductId}, OrderId: {OrderId}, CustomerId: {CustomerId}, CorrelationId: {CorrelationId}",
                            item.ProductId,
                            orderEvent.OrderId,
                            orderEvent.CustomerId,
                            correlationId);
                        throw;
                    }
                }

                if (failedItems.Any())
                {
                    _logger.LogWarning("[WARN] Alguns itens falharam durante o processamento. Não será gravada entrada de idempotência.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                // mark message as processed for idempotency
                var processedMessage = new ProcessedMessage(messageId);
                await dbContext.ProcessedMessages.AddAsync(processedMessage, stoppingToken);
                await dbContext.SaveChangesAsync(stoppingToken);

                _channel.BasicAck(ea.DeliveryTag, false);
                if (orderEvent.CreatedAt != default)
                {
                    var latencyMs = (DateTime.UtcNow - orderEvent.CreatedAt.ToUniversalTime()).TotalMilliseconds;
                    _metrics.TrackEventLatency(latencyMs, orderEvent.OrderId);
                }

                _logger.LogInformation(
                    "[INFO] Evento OrderConfirmed processado com sucesso. OrderId: {OrderId}, CustomerId: {CustomerId}, CorrelationId: {CorrelationId}",
                    orderEvent.OrderId,
                    orderEvent.CustomerId,
                    correlationId);

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(
                    ex,
                    "[ERRO] Erro ao processar mensagem RabbitMQ. OrderId: {OrderId}, CustomerId: {CustomerId}, CorrelationId: {CorrelationId}",
                    orderEvent?.OrderId,
                    orderEvent?.CustomerId,
                    correlationId);
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
            finally
            {
                activity?.Dispose();
            }
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("[INFO] RabbitMQ consumer iniciado e aguardando mensagens...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static PropagationContext ExtractPropagationContext(IBasicProperties? properties)
    {
        var headers = properties?.Headers;
        return Propagator.Extract(default, headers, static (dict, key) =>
        {
            if (dict == null)
            {
                return Array.Empty<string>();
            }

            if (!dict.TryGetValue(key, out var value) || value == null)
            {
                return Array.Empty<string>();
            }

            return value switch
            {
                byte[] bytes => new[] { Encoding.UTF8.GetString(bytes) },
                string str => new[] { str },
                _ => new[] { value.ToString() ?? string.Empty }
            };
        });
    }

    private static string? GetHeaderValue(IBasicProperties? properties, string key)
    {
        if (properties?.Headers == null)
        {
            return null;
        }

        if (!properties.Headers.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string str => str,
            _ => value.ToString()
        };
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
        _logger.LogInformation("RabbitMQ consumer descartado");
    }
}

public sealed class RabbitMqConsumerSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "inventory.stock-update";
    public string ExchangeName { get; set; } = "sales.events";
    public string ExchangeType { get; set; } = "topic";
    public string RoutingKey { get; set; } = "order.confirmed";
}
