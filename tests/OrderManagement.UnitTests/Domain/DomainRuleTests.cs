using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Rules;

namespace OrderManagement.UnitTests.Domain;

public class OrderTransitionTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    public void Allowed_transitions_are_accepted(OrderStatus from, OrderStatus to)
    {
        Assert.True(OrderTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending)]
    public void Disallowed_transitions_are_rejected(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderTransitions.CanTransition(from, to));
    }

    [Fact]
    public void Only_cancellation_restores_inventory()
    {
        Assert.True(OrderTransitions.RestoresInventory(OrderStatus.Cancelled));
        Assert.False(OrderTransitions.RestoresInventory(OrderStatus.Confirmed));
        Assert.False(OrderTransitions.RestoresInventory(OrderStatus.Shipped));
    }
}

public class MoneyCalculatorTests
{
    [Fact]
    public void Line_total_multiplies_captured_price_by_quantity()
    {
        Assert.Equal(25.00m, MoneyCalculator.LineTotal(12.50m, 2));
    }

    [Fact]
    public void Order_total_is_the_sum_of_line_totals()
    {
        Assert.Equal(28.00m, MoneyCalculator.Sum([25.00m, 3.00m]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_price_is_rejected(int price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MoneyCalculator.LineTotal(price, 1));
    }

    [Fact]
    public void Non_positive_quantity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MoneyCalculator.LineTotal(10m, 0));
    }
}

public class EmailNormalizerTests
{
    [Fact]
    public void Email_is_trimmed_and_lowercased()
    {
        Assert.Equal("ada@example.com", EmailNormalizer.Normalize("  Ada@Example.com  "));
    }
}
