using OrderManagement.Domain.Enums;

namespace OrderManagement.Domain.Rules;

/// <summary>
/// Allowed order transitions:
/// Pending → Confirmed, Pending → Cancelled, Confirmed → Shipped, Confirmed → Cancelled.
/// Shipped and Cancelled are terminal.
/// </summary>
public static class OrderTransitions
{
    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            _ => false
        };

    public static bool RestoresInventory(OrderStatus to) => to == OrderStatus.Cancelled;
}
