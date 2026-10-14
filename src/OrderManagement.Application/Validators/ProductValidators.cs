using FluentValidation;
using OrderManagement.Application.DTOs;
using OrderManagement.Domain.Rules;

namespace OrderManagement.Application.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(MoneyCalculator.MaxProductPrice)
            .Must(HasAtMostTwoDecimalPlaces)
            .WithMessage("Price must have at most two decimal places.");

        RuleFor(x => x.AvailableQuantity)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000);
    }

    internal static bool HasAtMostTwoDecimalPlaces(decimal value) => decimal.Round(value, 2) == value;
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductPriceRequestValidator : AbstractValidator<UpdateProductPriceRequest>
{
    public UpdateProductPriceRequestValidator()
    {
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(MoneyCalculator.MaxProductPrice)
            .Must(CreateProductRequestValidator.HasAtMostTwoDecimalPlaces)
            .WithMessage("Price must have at most two decimal places.");

        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateInventoryRequestValidator : AbstractValidator<UpdateInventoryRequest>
{
    public UpdateInventoryRequestValidator()
    {
        RuleFor(x => x.AvailableQuantity)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    private static readonly string[] SortFields = ["name", "price", "createdat"];

    public ProductQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.SortBy)
            .Must(value => SortFields.Contains(value.Trim().ToLowerInvariant()))
            .WithMessage("SortBy must be one of: name, price, createdAt.");
        RuleFor(x => x.SortDirection)
            .Must(value => value.Trim().ToLowerInvariant() is "asc" or "desc")
            .WithMessage("SortDirection must be asc or desc.");
    }
}
