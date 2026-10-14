namespace OrderManagement.Domain.Enums;

/// <summary>
/// Order lifecycle status. Persisted as the enum name (Pending, Confirmed, Shipped, Cancelled).
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Cancelled = 3
}
