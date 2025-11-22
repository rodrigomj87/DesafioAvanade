using System.Text;
using System.Text.Json;
using Inventory.Application.Services;
using Inventory.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Inventory.Infrastructure.Messaging;

public sealed class RabbitMqConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly RabbitMqConsumerSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumer(
        IServiceProvider serviceProvider,
        ILogger<RabbitMqConsumer> logger,
        RabbitMqConsumerSettings settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings;
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
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var orderEvent = JsonSerializer.Deserialize<OrderConfirmedEvent>(message, new JsonSerializerOptions
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
                    "[INFO] Evento OrderConfirmed recebido. OrderId: {OrderId}, Items: {ItemCount}",
                    orderEvent.OrderId,
                    orderEvent.Items.Count);

                using var scope = _serviceProvider.CreateScope();
                var stockMovementService = scope.ServiceProvider.GetRequiredService<StockMovementService>();

                foreach (var item in orderEvent.Items)
                {
                    try
                    {
                        await stockMovementService.RegisterAsync(new Inventory.Application.Contracts.RegisterStockMovementDto(
                            item.ProductId,
                            StockMovementType.Out,
                            item.Quantity,
                            $"Venda - Pedido {orderEvent.OrderId}",
                            orderEvent.OrderId.ToString()
                        ), stoppingToken);

                        _logger.LogInformation(
                            "[INFO] Baixa de estoque registrada. ProductId: {ProductId}, Quantity: {Quantity}",
                            item.ProductId,
                            item.Quantity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "[ERRO] Erro ao processar item do pedido. ProductId: {ProductId}, OrderId: {OrderId}",
                            item.ProductId,
                            orderEvent.OrderId);
                        throw;
                    }
                }

                _channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation("[INFO] Evento OrderConfirmed processado com sucesso. OrderId: {OrderId}", orderEvent.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERRO] Erro ao processar mensagem RabbitMQ");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("[INFO] RabbitMQ consumer iniciado e aguardando mensagens...");

        await Task.Delay(Timeout.Infinite, stoppingToken);
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
