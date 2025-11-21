using FluentAssertions;
using Inventory.Application.Contracts;
using Inventory.Application.Services;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Exceptions;
using Inventory.Domain.Repositories;
using NSubstitute;

namespace Inventory.Tests.Services;

public sealed class ProductServiceTests
{
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _productRepository = Substitute.For<IProductRepository>();
        _stockMovementRepository = Substitute.For<IStockMovementRepository>();
        _sut = new ProductService(_productRepository, _stockMovementRepository);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateProductAndStockMovement()
    {
        var dto = new CreateProductDto("TEST-001", "Produto Teste", "Descrição", 99.90m, 10);
        var expectedProduct = new Product(Guid.NewGuid(), dto.Sku, dto.Name, dto.Description, dto.Price, dto.QuantityAvailable);

        _productRepository.GetBySkuAsync(dto.Sku, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Product?>(null));
        _productRepository.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(expectedProduct);

        var result = await _sut.CreateAsync(dto, CancellationToken.None);

        result.Should().NotBeNull();
        result.Sku.Should().Be(dto.Sku);
        result.Name.Should().Be(dto.Name);
        result.Price.Should().Be(dto.Price);
        result.QuantityAvailable.Should().Be(dto.QuantityAvailable);

        await _productRepository.Received(1).GetBySkuAsync(dto.Sku, Arg.Any<CancellationToken>());
        await _productRepository.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _stockMovementRepository.Received(1).AddAsync(
            Arg.Is<StockMovement>(m => m.Type == StockMovementType.In && m.Quantity == dto.QuantityAvailable),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithZeroQuantity_ShouldNotCreateStockMovement()
    {
        var dto = new CreateProductDto("TEST-002", "Produto Teste", "Descrição", 99.90m, 0);
        var expectedProduct = new Product(Guid.NewGuid(), dto.Sku, dto.Name, dto.Description, dto.Price, dto.QuantityAvailable);

        _productRepository.GetBySkuAsync(dto.Sku, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Product?>(null));
        _productRepository.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(expectedProduct);

        var result = await _sut.CreateAsync(dto, CancellationToken.None);

        result.Should().NotBeNull();
        await _stockMovementRepository.DidNotReceive().AddAsync(Arg.Any<StockMovement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_ShouldThrowDuplicateSkuException()
    {
        var dto = new CreateProductDto("TEST-001", "Produto Teste", "Descrição", 99.90m, 10);
        var existingProduct = new Product(Guid.NewGuid(), dto.Sku, "Produto Existente", "Descrição antiga", 50m, 5);

        _productRepository.GetBySkuAsync(dto.Sku, Arg.Any<CancellationToken>())
            .Returns(existingProduct);

        var act = async () => await _sut.CreateAsync(dto, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateSkuException>()
            .WithMessage($"Produto com SKU '{dto.Sku}' já existe.");

        await _productRepository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPagedAsync_WithoutFilters_ShouldReturnPagedResult()
    {
        var products = new List<Product>
        {
            new(Guid.NewGuid(), "PROD-001", "Produto 1", "Desc 1", 10m, 5),
            new(Guid.NewGuid(), "PROD-002", "Produto 2", "Desc 2", 20m, 10)
        };
        var parameters = new ProductQueryParameters(Page: 1, PageSize: 10);

        _productRepository.GetPagedAsync(1, 10, null, null, null, Arg.Any<CancellationToken>())
            .Returns((products, 2));

        var result = await _sut.GetPagedAsync(parameters, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_WithFilters_ShouldReturnFilteredResult()
    {
        var products = new List<Product>
        {
            new(Guid.NewGuid(), "PROD-001", "Produto 1", "Desc 1", 10m, 5)
        };
        var parameters = new ProductQueryParameters(Page: 1, PageSize: 10, Sku: "PROD-001", Status: ProductStatus.Active);

        _productRepository.GetPagedAsync(1, 10, "PROD-001", null, (int)ProductStatus.Active, Arg.Any<CancellationToken>())
            .Returns((products, 1));

        var result = await _sut.GetPagedAsync(parameters, CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Sku.Should().Be("PROD-001");
    }
}
