using OrderManagement.Application.DTOs;
using OrderManagement.Application.Validators;

namespace OrderManagement.UnitTests.Validators;

public class ValidatorTests
{
    [Fact]
    public void Registration_rejects_an_invalid_email()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest
        {
            Name = "Ada",
            Email = "not-an-email",
            Password = "Password123"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "Email");
    }

    [Fact]
    public void Product_creation_rejects_a_non_positive_price_and_negative_inventory()
    {
        var result = new CreateProductRequestValidator().Validate(new CreateProductRequest
        {
            Name = "Desk",
            Price = 0,
            AvailableQuantity = -1
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "Price");
        Assert.Contains(result.Errors, error => error.PropertyName == "AvailableQuantity");
    }

    [Fact]
    public void Order_creation_rejects_an_empty_item_list_and_a_non_positive_quantity()
    {
        var empty = new CreateOrderRequestValidator().Validate(new CreateOrderRequest());
        Assert.Contains(empty.Errors, error => error.PropertyName == "Items");

        var quantity = new CreateOrderRequestValidator().Validate(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 1, Quantity = 0 }]
        });
        Assert.Contains(quantity.Errors, error => error.PropertyName.Contains("Quantity", StringComparison.Ordinal));
    }

    [Fact]
    public void Product_query_rejects_an_unknown_sort_field()
    {
        var result = new ProductQueryValidator().Validate(new ProductQuery
        {
            SortBy = "name; drop table products"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Order_query_rejects_an_unknown_status()
    {
        var result = new OrderQueryValidator().Validate(new OrderQuery { Status = "Delivered" });
        Assert.False(result.IsValid);
    }
}
