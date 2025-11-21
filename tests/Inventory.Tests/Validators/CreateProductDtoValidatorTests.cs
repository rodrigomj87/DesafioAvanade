using FluentAssertions;
using FluentValidation.TestHelper;
using Inventory.Application.Contracts;
using Inventory.Application.Validators;

namespace Inventory.Tests.Validators;

public sealed class CreateProductDtoValidatorTests
{
    private readonly CreateProductDtoValidator _validator;

    public CreateProductDtoValidatorTests()
    {
        _validator = new CreateProductDtoValidator();
    }

    [Fact]
    public void Validate_WithValidData_ShouldNotHaveErrors()
    {
        var dto = new CreateProductDto("TEST-001", "Produto Teste", "Descrição válida", 99.90m, 10);

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_WithEmptySku_ShouldHaveError(string sku)
    {
        var dto = new CreateProductDto(sku, "Produto", "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorMessage("SKU é obrigatório.");
    }

    [Fact]
    public void Validate_WithSkuTooLong_ShouldHaveError()
    {
        var sku = new string('A', 65);
        var dto = new CreateProductDto(sku, "Produto", "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorMessage("SKU deve ter no máximo 64 caracteres.");
    }

    [Theory]
    [InlineData("test-001")]
    [InlineData("TEST 001")]
    [InlineData("TEST@001")]
    public void Validate_WithInvalidSkuFormat_ShouldHaveError(string sku)
    {
        var dto = new CreateProductDto(sku, "Produto", "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorMessage("SKU deve conter apenas letras maiúsculas, números e hífens.");
    }

    [Fact]
    public void Validate_WithValidSkuFormat_ShouldNotHaveError()
    {
        var dto = new CreateProductDto("TEST-001", "Produto", "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Sku);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithEmptyName_ShouldHaveError(string name)
    {
        var dto = new CreateProductDto("TEST-001", name, "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Nome é obrigatório.");
    }

    [Fact]
    public void Validate_WithNameTooLong_ShouldHaveError()
    {
        var name = new string('A', 181);
        var dto = new CreateProductDto("TEST-001", name, "Descrição", 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Nome deve ter no máximo 180 caracteres.");
    }

    [Fact]
    public void Validate_WithDescriptionTooLong_ShouldHaveError()
    {
        var description = new string('A', 1025);
        var dto = new CreateProductDto("TEST-001", "Produto", description, 10m, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Descrição deve ter no máximo 1024 caracteres.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99.99)]
    public void Validate_WithInvalidPrice_ShouldHaveError(decimal price)
    {
        var dto = new CreateProductDto("TEST-001", "Produto", "Descrição", price, 5);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Preço deve ser maior que zero.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_WithNegativeQuantity_ShouldHaveError(int quantity)
    {
        var dto = new CreateProductDto("TEST-001", "Produto", "Descrição", 10m, quantity);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.QuantityAvailable)
            .WithErrorMessage("Quantidade deve ser maior ou igual a zero.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_WithValidQuantity_ShouldNotHaveError(int quantity)
    {
        var dto = new CreateProductDto("TEST-001", "Produto", "Descrição", 10m, quantity);

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.QuantityAvailable);
    }
}
