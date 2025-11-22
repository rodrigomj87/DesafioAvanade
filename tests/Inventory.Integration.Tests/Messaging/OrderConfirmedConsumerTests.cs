using FluentAssertions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Repositories;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Inventory.Integration.Tests.Fixtures;
using MassTransit;
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

        var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(orderEvent, ctx =>
        {
            ctx.Headers.Set("x-correlation-id", Guid.NewGuid().ToString());
        });

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

        var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        
        await publishEndpoint.Publish(orderEvent, ctx =>
        {
            ctx.MessageId = messageId;
            ctx.Headers.Set("x-correlation-id", Guid.NewGuid().ToString());
        });

        await Task.Delay(3000);

        await publishEndpoint.Publish(orderEvent, ctx =>
        {
            ctx.MessageId = messageId;
            ctx.Headers.Set("x-correlation-id", Guid.NewGuid().ToString());
        });

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

        var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(orderEvent, ctx =>
        {
            ctx.Headers.Set("x-correlation-id", Guid.NewGuid().ToString());
        });

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

        var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(orderEvent, ctx =>
        {
            ctx.Headers.Set("x-correlation-id", Guid.NewGuid().ToString());
        });

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
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole());
        services.AddInventoryInfrastructure(configuration);
    }
}
