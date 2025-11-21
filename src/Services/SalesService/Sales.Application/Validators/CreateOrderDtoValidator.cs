using FluentValidation;
using Sales.Application.Contracts;

namespace Sales.Application.Validators;

public sealed class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required")
            .MaximumLength(200)
            .WithMessage("CustomerId cannot exceed 200 characters");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item")
            .Must(items => items != null && items.Count > 0)
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemDtoValidator());
    }
}

public sealed class OrderItemDtoValidator : AbstractValidator<OrderItemDto>
{
    public OrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required");

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .WithMessage("ProductName is required")
            .MaximumLength(200)
            .WithMessage("ProductName cannot exceed 200 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero")
            .LessThanOrEqualTo(10000)
            .WithMessage("Quantity cannot exceed 10000");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("UnitPrice must be greater than zero")
            .LessThanOrEqualTo(1000000)
            .WithMessage("UnitPrice cannot exceed 1,000,000");
    }
}
