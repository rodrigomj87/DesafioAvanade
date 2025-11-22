using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Sales.Application.Contracts;
using Sales.Application.Events;
using Sales.Application.Services;
using Sales.Domain.Entities;
using Sales.Domain.Repositories;
using Sales.Infrastructure.Messaging;
using Sales.Infrastructure.Observability;
using Sales.Infrastructure.Services;
using Xunit;

namespace Sales.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IStockChecker> _mockStockChecker;
    private readonly Mock<IValidator<CreateOrderDto>> _mockValidator;
    private readonly Mock<IRabbitMqPublisher> _mockPublisher;
    private readonly Mock<ILogger<OrderService>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly SalesMetrics _salesMetrics;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockStockChecker = new Mock<IStockChecker>();
        _mockValidator = new Mock<IValidator<CreateOrderDto>>();
        _mockPublisher = new Mock<IRabbitMqPublisher>();
        _mockLogger = new Mock<ILogger<OrderService>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
        _salesMetrics = new SalesMetrics();

        _orderService = new OrderService(
            _mockOrderRepository.Object,
            _mockStockChecker.Object,
            _mockValidator.Object,
            _mockPublisher.Object,
            _mockLogger.Object,
            _salesMetrics,
            _mockHttpContextAccessor.Object
        );
    }

    private void SetupSuccessfulStockCheck()
    {
        _mockStockChecker
            .Setup(s => s.CheckMultipleAsync(It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<Guid, int> products, CancellationToken _) =>
                products.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new StockCheckResult(kvp.Key, true, kvp.Value * 10, null)
                ));
    }

    [Fact]
    public async Task CreateOrderAsync_Should_Publish_OrderConfirmedEvent_With_Correct_Payload()
    {
        var dto = new CreateOrderDto(
            "CUST-123",
            new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product A", 2, 10.50m),
                new(Guid.NewGuid(), "Product B", 1, 20.00m)
            }
        );

        _mockValidator
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        SetupSuccessfulStockCheck();

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orderService.CreateOrderAsync(dto, CancellationToken.None);

        _mockPublisher.Verify(
            p => p.PublishAsync(
                It.IsAny<OrderConfirmedEvent>(),
                "order.confirmed",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateOrderAsync_Should_Include_Correlation_And_Source_Headers()
    {
        var dto = new CreateOrderDto(
            "CUST-456",
            new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product X", 1, 15.00m)
            }
        );

        _mockValidator
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        SetupSuccessfulStockCheck();

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orderService.CreateOrderAsync(dto, CancellationToken.None);

        _mockPublisher.Verify(
            p => p.PublishAsync(
                It.IsAny<object>(),
                "order.confirmed",
                It.Is<Dictionary<string, object>>(headers =>
                    headers.ContainsKey("x-source-service") &&
                    headers["x-source-service"].ToString() == "sales-service" &&
                    headers.ContainsKey("x-correlation-id")
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateOrderAsync_Should_Log_Success_When_Event_Published()
    {
        var dto = new CreateOrderDto(
            "CUST-789",
            new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product Y", 3, 5.00m)
            }
        );

        _mockValidator
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        SetupSuccessfulStockCheck();

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orderService.CreateOrderAsync(dto, CancellationToken.None);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Order confirmed event published")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateOrderAsync_Should_Log_Error_When_Publishing_Fails()
    {
        var dto = new CreateOrderDto(
            "CUST-ERROR",
            new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product Z", 1, 10.00m)
            }
        );

        _mockValidator
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        SetupSuccessfulStockCheck();

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publishException = new Exception("RabbitMQ connection failed");
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(publishException);

        var result = await _orderService.CreateOrderAsync(dto, CancellationToken.None);

        Assert.NotNull(result);
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish order confirmed event")),
                It.Is<Exception>(ex => ex == publishException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateOrderAsync_Should_Use_Correct_Routing_Key()
    {
        var dto = new CreateOrderDto(
            "CUST-ROUTING",
            new List<OrderItemDto>
            {
                new(Guid.NewGuid(), "Product R", 1, 25.00m)
            }
        );

        _mockValidator
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        SetupSuccessfulStockCheck();

        _mockOrderRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orderService.CreateOrderAsync(dto, CancellationToken.None);

        _mockPublisher.Verify(
            p => p.PublishAsync(
                It.IsAny<object>(),
                "order.confirmed",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
}
