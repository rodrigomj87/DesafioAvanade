using FluentValidation;
using Inventory.Application.Contracts;

namespace Inventory.Application.Validators;

public sealed class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU é obrigatório.")
            .MaximumLength(64).WithMessage("SKU deve ter no máximo 64 caracteres.")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU deve conter apenas letras maiúsculas, números e hífens.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(180).WithMessage("Nome deve ter no máximo 180 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Descrição deve ter no máximo 1024 caracteres.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Preço deve ser maior que zero.");

        RuleFor(x => x.QuantityAvailable)
            .GreaterThanOrEqualTo(0).WithMessage("Quantidade deve ser maior ou igual a zero.");
    }
}
