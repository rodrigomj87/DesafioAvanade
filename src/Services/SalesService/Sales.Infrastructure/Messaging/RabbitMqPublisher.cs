using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry.Context.Propagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Sales.Infrastructure.Observability;
using OpenTelemetry;

namespace Sales.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(T message, string routingKey, Dictionary<string, object>? headers = null, CancellationToken cancellationToken = default) where T : class;
}

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private bool _disposed;

    public RabbitMqPublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            VirtualHost = _settings.VirtualHost,
            UserName = _settings.Username,
            Password = _settings.Password,
            DispatchConsumersAsync = true
        };

        var maxRetries = 5;
        var delay = TimeSpan.FromSeconds(2);
        IConnection? connection = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                connection = factory.CreateConnection();
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "[ERRO]  Tentativa {Attempt}/{MaxRetries}: Não foi possível conectar ao RabbitMQ, tentando novamente em {Delay}s... (Host: {Host}:{Port}) - " + ex.Message,
                    attempt,
                    maxRetries,
                    delay.TotalSeconds,
                    _settings.Host,
                    _settings.Port);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[ERRO] Falha ao conectar ao RabbitMQ após {MaxRetries} tentativas. Host: {Host}:{Port}",
                    maxRetries,
                    _settings.Host,
                    _settings.Port);
                throw;
            }
        }

        if (connection == null)
        {
            throw new InvalidOperationException($"Não foi possível conectar ao RabbitMQ após {maxRetries} tentativas. Verifique se o RabbitMQ está rodando em {_settings.Host}:{_settings.Port}");
        }

        _connection = connection;
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: _settings.ExchangeName,
            type: _settings.ExchangeType,
            durable: _settings.Durable,
            autoDelete: false,
            arguments: null);

        _logger.LogInformation(
            "RabbitMQ publisher inicializado. Exchange: {Exchange}, Type: {Type}, Host: {Host}:{Port}",
            _settings.ExchangeName,
            _settings.ExchangeType,
            _settings.Host,
            _settings.Port);
    }

    public async Task PublishAsync<T>(
        T message,
        string routingKey,
        Dictionary<string, object>? headers = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var retryCount = 0;
        var currentInterval = TimeSpan.FromSeconds(_settings.RetryIntervalSeconds);

        while (retryCount <= _settings.RetryCount)
        {
            Activity? activity = null;
            try
            {
                var parentContext = Activity.Current?.Context ?? default;
                activity = SalesTelemetry.ActivitySource.StartActivity("RabbitMQ Publish", ActivityKind.Producer, parentContext);
                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination", _settings.ExchangeName);
                activity?.SetTag("messaging.rabbitmq.routing_key", routingKey);

                var body = SerializeMessage(message);
                var properties = _channel.CreateBasicProperties();
                properties.ContentType = "application/json";
                properties.DeliveryMode = 2;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Headers = headers != null
                    ? new Dictionary<string, object>(headers)
                    : new Dictionary<string, object>();

                if (!properties.Headers.ContainsKey("x-source-service"))
                {
                    properties.Headers["x-source-service"] = "sales-service";
                }

                var correlationId = EnsureCorrelationId(properties.Headers);
                properties.CorrelationId = correlationId;

                InjectTraceContext(properties, activity);
                activity?.SetTag("messaging.message_id", properties.MessageId);
                activity?.SetTag("messaging.conversation_id", correlationId);

                _channel.BasicPublish(
                    exchange: _settings.ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                await Task.CompletedTask;

                _logger.LogInformation(
                    "Mensagem publicada com sucesso. Exchange: {Exchange}, RoutingKey: {RoutingKey}, MessageId: {MessageId}, CorrelationId: {CorrelationId}",
                    _settings.ExchangeName,
                    routingKey,
                    properties.MessageId,
                    correlationId);

                activity?.SetStatus(ActivityStatusCode.Ok);

                return;
            }
            catch (Exception ex) when (retryCount < _settings.RetryCount)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                retryCount++;
                _logger.LogWarning(
                    ex,
                    "Erro ao publicar mensagem. Tentativa {RetryCount}/{MaxRetries}. Aguardando {Interval}s antes de retentar. RoutingKey: {RoutingKey}",
                    retryCount,
                    _settings.RetryCount,
                    currentInterval.TotalSeconds,
                    routingKey);

                await Task.Delay(currentInterval, cancellationToken);

                if (_settings.UseExponentialBackoff)
                {
                    currentInterval = TimeSpan.FromSeconds(currentInterval.TotalSeconds * 2);
                }
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(
                    ex,
                    "Falha definitiva ao publicar mensagem após {RetryCount} tentativas. RoutingKey: {RoutingKey}",
                    _settings.RetryCount,
                    routingKey);
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }
    }

    private static void InjectTraceContext(IBasicProperties properties, Activity? activity)
    {
        properties.Headers ??= new Dictionary<string, object>();

        var propagationContext = new PropagationContext(
            activity?.Context ?? Activity.Current?.Context ?? default,
            default);

        Propagator.Inject(propagationContext, properties.Headers, static (headers, key, value) =>
        {
            headers[key] = value;
        });
    }

    private static string EnsureCorrelationId(IDictionary<string, object> headers)
    {
        if (headers.TryGetValue("x-correlation-id", out var existing))
        {
            return existing switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string str => str,
                _ => existing?.ToString() ?? Guid.NewGuid().ToString()
            };
        }

        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
        headers["x-correlation-id"] = correlationId;
        return correlationId;
    }

    private static byte[] SerializeMessage<T>(T message) where T : class
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        return Encoding.UTF8.GetBytes(json);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _channel?.Dispose();
        _connection?.Dispose();
        _disposed = true;

        _logger.LogInformation("RabbitMQ publisher descartado.");
    }
}
