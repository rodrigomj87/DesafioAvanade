using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using MassTransit;
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

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderConfirmedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("RabbitMq");
                cfg.Host(rabbitMqConfig["Host"] ?? "localhost", h =>
                {
                    h.Username(rabbitMqConfig["Username"] ?? "guest");
                    h.Password(rabbitMqConfig["Password"] ?? "guest");
                });

                cfg.ReceiveEndpoint("inventory.order-confirmed", e =>
                {
                    e.ConfigureConsumer<OrderConfirmedConsumer>(context);

                    e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2)));

                    e.Bind("sales.events", s =>
                    {
                        s.RoutingKey = "order.confirmed";
                        s.ExchangeType = "topic";
                    });
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
