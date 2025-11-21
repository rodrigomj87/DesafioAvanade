namespace Sales.Infrastructure.Messaging;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "sales.events";
    public string ExchangeType { get; set; } = "topic";
    public bool Durable { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetryIntervalSeconds { get; set; } = 2;
    public bool UseExponentialBackoff { get; set; } = true;
}
