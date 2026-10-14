using Microsoft.Extensions.Logging;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Mapping;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Rules;

namespace OrderManagement.Application.Services;

public sealed class OrderService(
    IOrderRepository orders,
    IProductRepository products,
    ICustomerRepository customers,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<OrderService> logger) : IOrderService
{
    public Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsCustomer)
        {
            throw new ForbiddenException("Only customers can place orders.");
        }

        var customerId = currentUser.GetUserId();
        return unitOfWork.ExecuteAsync(
            async token => await CreateInTransactionAsync(customerId, request, token),
            cancellationToken);
    }

    public async Task<PagedResponse<OrderResponse>> ListAsync(OrderQuery query, CancellationToken cancellationToken)
    {
        int? customerId = currentUser.IsAdmin ? null : currentUser.GetUserId();
        var page = await orders.ListAsync(query, customerId, cancellationToken);
        return ResponseMapper.ToPagedResponse(page, ResponseMapper.ToResponse);
    }

    public async Task<OrderResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(id, cancellationToken);
        if (order is null || (!currentUser.IsAdmin && order.CustomerId != currentUser.GetUserId()))
        {
            throw new NotFoundException("Order", id);
        }

        return ResponseMapper.ToResponse(order);
    }

    public Task<OrderResponse> ConfirmAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, OrderStatus.Confirmed, adminOnly: true, cancellationToken);

    public Task<OrderResponse> ShipAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, OrderStatus.Shipped, adminOnly: true, cancellationToken);

    public Task<OrderResponse> CancelAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, OrderStatus.Cancelled, adminOnly: false, cancellationToken);

    private async Task<OrderResponse> CreateInTransactionAsync(
        int customerId,
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("Customer", customerId);
        }

        if (!customer.IsActive)
        {
            throw new ConflictException("Inactive customers cannot place orders.", "inactive_customer");
        }

        var lines = Aggregate(request.Items);
        var now = clock.UtcNow;
        var items = new List<OrderItem>(lines.Count);

        foreach (var (productId, quantity) in lines)
        {
            var reservation = await products.TryReserveAsync(productId, quantity, now, cancellationToken);
            if (!reservation.Succeeded)
            {
                logger.LogWarning(
                    "Inventory reservation failed for product {ProductId}. Reason: {Reason}.",
                    productId,
                    reservation.Failure);
                throw MapReservationFailure(productId, reservation.Failure);
            }

            var snapshot = await products.GetPriceAndNameAsync(productId, cancellationToken);
            if (snapshot is null)
            {
                throw new NotFoundException("Product", productId);
            }

            decimal lineTotal;
            try
            {
                lineTotal = MoneyCalculator.LineTotal(snapshot.Value.Price, quantity);
            }
            catch (OverflowException)
            {
                throw new ConflictException("The order total exceeds the supported monetary range.", "amount_overflow");
            }

            items.Add(new OrderItem
            {
                ProductId = productId,
                ProductName = snapshot.Value.Name,
                Quantity = quantity,
                UnitPrice = snapshot.Value.Price,
                LineTotal = lineTotal
            });
        }

        decimal total;
        try
        {
            total = MoneyCalculator.Sum(items.Select(item => item.LineTotal));
        }
        catch (OverflowException)
        {
            throw new ConflictException("The order total exceeds the supported monetary range.", "amount_overflow");
        }

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            TotalAmount = total,
            CreatedAt = now,
            UpdatedAt = now,
            Items = items
        };

        await orders.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId} with total {TotalAmount} and {ItemCount} item lines.",
            order.Id,
            customerId,
            order.TotalAmount,
            items.Count);
        return ResponseMapper.ToResponse(order);
    }

    private Task<OrderResponse> TransitionAsync(
        int orderId,
        OrderStatus target,
        bool adminOnly,
        CancellationToken cancellationToken)
    {
        if (adminOnly && !currentUser.IsAdmin)
        {
            throw new ForbiddenException("Only administrators can change this order status.");
        }

        var userId = currentUser.GetUserId();
        var isAdmin = currentUser.IsAdmin;
        return unitOfWork.ExecuteAsync(
            token => TransitionInTransactionAsync(orderId, target, userId, isAdmin, token),
            cancellationToken);
    }

    private async Task<OrderResponse> TransitionInTransactionAsync(
        int orderId,
        OrderStatus target,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || (!isAdmin && order.CustomerId != userId))
        {
            throw new NotFoundException("Order", orderId);
        }

        if (!OrderTransitions.CanTransition(order.Status, target))
        {
            logger.LogWarning(
                "Rejected transition of order {OrderId} from {FromStatus} to {ToStatus}.",
                orderId,
                order.Status,
                target);
            throw new ConflictException(
                $"An order cannot move from {order.Status} to {target}.",
                "invalid_transition");
        }

        var expected = order.Status;
        var now = clock.UtcNow;
        var changed = await orders.TryTransitionAsync(orderId, expected, target, now, cancellationToken);
        if (!changed)
        {
            logger.LogWarning("Concurrent transition conflict for order {OrderId}.", orderId);
            throw new ConflictException(
                "The order was updated by another request.",
                "concurrency_conflict");
        }

        if (OrderTransitions.RestoresInventory(target))
        {
            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                await products.RestoreAsync(item.ProductId, item.Quantity, now, cancellationToken);
            }

            logger.LogInformation("Order {OrderId} cancelled by user {UserId}. Inventory restored.", orderId, userId);
        }
        else if (target == OrderStatus.Confirmed)
        {
            logger.LogInformation("Order {OrderId} confirmed.", orderId);
        }
        else if (target == OrderStatus.Shipped)
        {
            logger.LogInformation("Order {OrderId} shipped.", orderId);
        }

        order.Status = target;
        order.UpdatedAt = now;
        return ResponseMapper.ToResponse(order);
    }

    private static List<(int ProductId, int Quantity)> Aggregate(IReadOnlyList<CreateOrderItemRequest> items)
    {
        if (items.Count == 0)
        {
            throw new ConflictException("An order must contain at least one item.", "empty_order");
        }

        var quantities = new SortedDictionary<int, int>();
        foreach (var item in items)
        {
            if (item.ProductId <= 0)
            {
                throw new ConflictException("Each order item must reference a product.", "invalid_product");
            }

            if (item.Quantity <= 0)
            {
                throw new ConflictException("Quantity must be greater than zero.", "invalid_quantity");
            }

            quantities.TryGetValue(item.ProductId, out var current);
            try
            {
                quantities[item.ProductId] = checked(current + item.Quantity);
            }
            catch (OverflowException)
            {
                throw new ConflictException("Requested quantity is too large.", "quantity_overflow");
            }
        }

        return quantities.Select(pair => (pair.Key, pair.Value)).ToList();
    }

    private static Exception MapReservationFailure(int productId, InventoryReservationFailure failure) =>
        failure switch
        {
            InventoryReservationFailure.NotFound => new NotFoundException("Product", productId),
            InventoryReservationFailure.Inactive => new ConflictException(
                $"Product {productId} is not available for purchase.",
                "inactive_product"),
            _ => new ConflictException(
                $"Insufficient inventory is available for product {productId}.",
                "insufficient_inventory")
        };
}
