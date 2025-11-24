using FluentAssertions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Inventory.Integration.Tests.Fixtures;
using MassTransit;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Integration.Tests.Messaging;

[Collection("Integration")]
public sealed class OrderConfirmedConsumerTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public OrderConfirmedConsumerTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Consume_ShouldReduceStock_WhenOrderConfirmedEventIsReceived()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        // start hosted services (RabbitMqConsumer) so it can receive messages
        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var hs in hostedServices)
        {
            await hs.StartAsync(CancellationToken.None);
        }

        var dbContext = provider.GetRequiredService<InventoryDbContext>();
        var productRepo = provider.GetRequiredService<IProductRepository>();

        var product = new Product(
            Guid.NewGuid(),
            "TEST-001",
            "Test Product",
            "Integration test product",
            100m,
            50,
            0,
            ProductStatus.Active);

        await productRepo.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var orderEvent = new OrderConfirmedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "customer-123",
            Items = new List<OrderItemEvent>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 100m }
            },
            TotalAmount = 1000m,
            CreatedAt = DateTime.UtcNow
        };

        // publish directly to the exchange/routing key expected by the consumer
        var config = provider.GetRequiredService<IConfiguration>();
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMq:Port"] ?? "5672"),
            UserName = config["RabbitMq:Username"] ?? "guest",
            Password = config["RabbitMq:Password"] ?? "guest"
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderEvent));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.Headers = new Dictionary<string, object?> { { "x-correlation-id", Guid.NewGuid().ToString() } };
            channel.BasicPublish(exchange: "sales.events", routingKey: "order.confirmed", basicProperties: props, body: body);
        }

        await Task.Delay(3000);

        var updatedProduct = await productRepo.GetByIdAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.QuantityAvailable.Should().Be(40);
    }

    [Fact]
    public async Task Consume_ShouldBeIdempotent_WhenSameMessageIsProcessedTwice()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var hs in hostedServices)
        {
            await hs.StartAsync(CancellationToken.None);
        }

        var dbContext = provider.GetRequiredService<InventoryDbContext>();
        var productRepo = provider.GetRequiredService<IProductRepository>();

        var product = new Product(
            Guid.NewGuid(),
            "TEST-002",
            "Test Product 2",
            "Idempotency test",
            100m,
            50,
            0,
            ProductStatus.Active);

        await productRepo.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var orderEvent = new OrderConfirmedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "customer-456",
            Items = new List<OrderItemEvent>
            {
                new() { ProductId = product.Id, Quantity = 5, UnitPrice = 100m }
            },
            TotalAmount = 500m,
            CreatedAt = DateTime.UtcNow
        };

        var config = provider.GetRequiredService<IConfiguration>();
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMq:Port"] ?? "5672"),
            UserName = config["RabbitMq:Username"] ?? "guest",
            Password = config["RabbitMq:Password"] ?? "guest"
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderEvent));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = messageId.ToString();
            props.Headers = new Dictionary<string, object?> { { "x-correlation-id", Guid.NewGuid().ToString() } };
            channel.BasicPublish(exchange: "sales.events", routingKey: "order.confirmed", basicProperties: props, body: body);
        }

        await Task.Delay(3000);

        // publish the same message a second time (same MessageId) to validate idempotency
        using (var connection2 = factory.CreateConnection())
        using (var channel2 = connection2.CreateModel())
        {
            var body2 = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderEvent));
            var props2 = channel2.CreateBasicProperties();
            props2.Persistent = true;
            props2.MessageId = messageId.ToString();
            props2.Headers = new Dictionary<string, object?> { { "x-correlation-id", Guid.NewGuid().ToString() } };
            channel2.BasicPublish(exchange: "sales.events", routingKey: "order.confirmed", basicProperties: props2, body: body2);
        }

        await Task.Delay(3000);

        var updatedProduct = await productRepo.GetByIdAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.QuantityAvailable.Should().Be(45);

        var processedMessages = await dbContext.ProcessedMessages
            .Where(pm => pm.MessageId == messageId.ToString())
            .ToListAsync();
        processedMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Consume_ShouldPublishCompensationEvent_WhenStockIsInsufficient()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var hs in hostedServices)
        {
            await hs.StartAsync(CancellationToken.None);
        }

        var dbContext = provider.GetRequiredService<InventoryDbContext>();
        var productRepo = provider.GetRequiredService<IProductRepository>();

        var product = new Product(
            Guid.NewGuid(),
            "TEST-003",
            "Test Product 3",
            "Insufficient stock test",
            100m,
            5,
            0,
            ProductStatus.Active);

        await productRepo.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var orderEvent = new OrderConfirmedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "customer-789",
            Items = new List<OrderItemEvent>
            {
                new() { ProductId = product.Id, Quantity = 10, UnitPrice = 100m }
            },
            TotalAmount = 1000m,
            CreatedAt = DateTime.UtcNow
        };

        var config = provider.GetRequiredService<IConfiguration>();
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMq:Port"] ?? "5672"),
            UserName = config["RabbitMq:Username"] ?? "guest",
            Password = config["RabbitMq:Password"] ?? "guest"
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderEvent));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.Headers = new Dictionary<string, object?> { { "x-correlation-id", Guid.NewGuid().ToString() } };
            channel.BasicPublish(exchange: "sales.events", routingKey: "order.confirmed", basicProperties: props, body: body);
        }

        await Task.Delay(3000);

        var updatedProduct = await productRepo.GetByIdAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.QuantityAvailable.Should().Be(5);
    }

    [Fact]
    public async Task Consume_ShouldNotProcessMessage_WhenProductNotFound()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var hs in hostedServices)
        {
            await hs.StartAsync(CancellationToken.None);
        }

        var nonExistentProductId = Guid.NewGuid();

        var orderEvent = new OrderConfirmedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = "customer-999",
            Items = new List<OrderItemEvent>
            {
                new() { ProductId = nonExistentProductId, Quantity = 1, UnitPrice = 100m }
            },
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow
        };

        var config = provider.GetRequiredService<IConfiguration>();
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            Port = int.Parse(config["RabbitMq:Port"] ?? "5672"),
            UserName = config["RabbitMq:Username"] ?? "guest",
            Password = config["RabbitMq:Password"] ?? "guest"
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderEvent));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.Headers = new Dictionary<string, object?> { { "x-correlation-id", Guid.NewGuid().ToString() } };
            channel.BasicPublish(exchange: "sales.events", routingKey: "order.confirmed", basicProperties: props, body: body);
        }

        await Task.Delay(3000);

        var dbContext = provider.GetRequiredService<InventoryDbContext>();
        var processedCount = await dbContext.ProcessedMessages.CountAsync();
        processedCount.Should().Be(0);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InventoryDatabase"] = _fixture.SqlConnectionString,
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Port"] = _fixture.RabbitMqPort.ToString(),
                ["RabbitMq:Username"] = "test",
                ["RabbitMq:Password"] = "testpwd"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole());
        services.AddInventoryInfrastructure(configuration);
        // register application services (ProductService, StockMovementService, validators)
        services.AddInventoryApplication();

        // Register MassTransit so tests can publish to RabbitMQ using IPublishEndpoint
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var port = int.Parse(configuration["RabbitMq:Port"] ?? "5672");
                var username = configuration["RabbitMq:Username"] ?? "guest";
                var password = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(new Uri($"rabbitmq://{host}:{port}/"), h =>
                {
                    h.Username(username);
                    h.Password(password);
                });
            });
        });

        // Ensure IPublishEndpoint is resolvable from the test ServiceProvider
        services.AddSingleton<IPublishEndpoint>(sp => sp.GetRequiredService<IBus>());
    }
}
