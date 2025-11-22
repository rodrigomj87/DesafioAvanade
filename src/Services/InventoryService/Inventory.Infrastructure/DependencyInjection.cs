using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using Inventory.Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class InventoryInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDatabase")
            ?? throw new InvalidOperationException("Connection string 'InventoryDatabase' not found.");

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IStockMovementRepository, EfStockMovementRepository>();
        services.AddSingleton<InventoryMetrics>();

        var rabbitMqConfig = configuration.GetSection("RabbitMq");
        services.AddSingleton(new Inventory.Infrastructure.Messaging.RabbitMqConsumerSettings
        {
            Host = rabbitMqConfig["Host"] ?? "localhost",
            Port = int.Parse(rabbitMqConfig["Port"] ?? "5672"),
            Username = rabbitMqConfig["Username"] ?? "guest",
            Password = rabbitMqConfig["Password"] ?? "guest",
            QueueName = rabbitMqConfig["QueueName"] ?? "inventory.stock-update",
            ExchangeName = rabbitMqConfig["ExchangeName"] ?? "sales.events",
            ExchangeType = rabbitMqConfig["ExchangeType"] ?? "topic",
            RoutingKey = rabbitMqConfig["RoutingKey"] ?? "order.confirmed"
        });
        services.AddHostedService<Inventory.Infrastructure.Messaging.RabbitMqConsumer>();

        return services;
    }
}
