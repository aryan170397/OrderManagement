namespace OrderManagement.Domain.Rules;

public static class MoneyCalculator
{
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public const decimal MaxProductPrice = 9_999_999.99m;

    public static decimal LineTotal(decimal unitPrice, int quantity)
    {
        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price must be greater than zero.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        decimal total;
        try
        {
            total = unitPrice * quantity;
        }
        catch (OverflowException)
        {
            throw new OverflowException("Line total exceeds the supported monetary range.");
        }

        if (total > MaxAmount)
        {
            throw new OverflowException("Line total exceeds the supported monetary range.");
        }

        return total;
    }

    public static decimal Sum(IEnumerable<decimal> lineTotals)
    {
        decimal total = 0m;
        foreach (var lineTotal in lineTotals)
        {
            try
            {
                total += lineTotal;
            }
            catch (OverflowException)
            {
                throw new OverflowException("Order total exceeds the supported monetary range.");
            }

            if (total > MaxAmount)
            {
                throw new OverflowException("Order total exceeds the supported monetary range.");
            }
        }

        return total;
    }
}
