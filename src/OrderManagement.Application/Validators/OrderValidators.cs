using FluentValidation;
using OrderManagement.Application.DTOs;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items is { Count: > 0 })
            .WithMessage("An order must contain at least one item.")
            .Must(items => items is null || items.Count <= 100)
            .WithMessage("An order cannot contain more than 100 lines.");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public sealed class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 10_000);
    }
}

public sealed class OrderQueryValidator : AbstractValidator<OrderQuery>
{
    public OrderQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || Enum.TryParse<OrderStatus>(status, ignoreCase: true, out _))
            .WithMessage("Status must be Pending, Confirmed, Shipped, or Cancelled.");
        RuleFor(x => x)
            .Must(query => query.CreatedFromUtc is null || query.CreatedToUtc is null || query.CreatedFromUtc <= query.CreatedToUtc)
            .WithMessage("CreatedFromUtc must be earlier than or equal to CreatedToUtc.");
    }
}
