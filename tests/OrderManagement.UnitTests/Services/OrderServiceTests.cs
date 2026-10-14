using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Services;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.UnitTests.Support;

namespace OrderManagement.UnitTests.Services;

public class OrderServiceTests
{
    [Fact]
    public async Task Order_captures_prices_and_calculates_totals_on_the_server()
    {
        var fixture = new ServiceFixture();
        var customer = fixture.AddCustomer();
        var first = fixture.AddProduct("Keyboard", 12.50m, 10);
        var second = fixture.AddProduct("Mouse", 3.00m, 10);
        var service = CreateService(fixture);

        var order = await service.CreateAsync(new CreateOrderRequest
        {
            Items =
            [
                new CreateOrderItemRequest { ProductId = first.Id, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = first.Id, Quantity = 1 },
                new CreateOrderItemRequest { ProductId = second.Id, Quantity = 1 }
            ]
        }, CancellationToken.None);

        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(OrderStatus.Pending.ToString(), order.Status);
        Assert.Equal(40.50m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        var keyboard = Assert.Single(order.Items, item => item.ProductId == first.Id);
        Assert.Equal(3, keyboard.Quantity);
        Assert.Equal(12.50m, keyboard.UnitPrice);
        Assert.Equal(37.50m, keyboard.LineTotal);
        Assert.Equal(7, first.AvailableQuantity);
        Assert.Equal(9, second.AvailableQuantity);

        first.Price = 99m;
        var stored = fixture.Store.Orders.Single();
        Assert.Equal(12.50m, stored.Items.Single(item => item.ProductId == first.Id).UnitPrice);
    }

    [Fact]
    public async Task Empty_order_and_non_positive_quantity_are_rejected()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var service = CreateService(fixture);

        var empty = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(new CreateOrderRequest(), CancellationToken.None));
        Assert.Equal("empty_order", empty.ErrorCode);

        var quantity = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 1, Quantity = 0 }]
        }, CancellationToken.None));
        Assert.Equal("invalid_quantity", quantity.ErrorCode);
    }

    [Fact]
    public async Task Missing_inactive_and_undersupplied_products_reject_the_whole_order()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var service = CreateService(fixture);

        var missing = await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 99, Quantity = 1 }]
        }, CancellationToken.None));
        Assert.Equal(99, missing.Message.Contains("99", StringComparison.Ordinal) ? 99 : 0);

        var inactive = fixture.AddProduct(active: false);
        var inactiveError = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = inactive.Id, Quantity = 1 }]
        }, CancellationToken.None));
        Assert.Equal("inactive_product", inactiveError.ErrorCode);

        var scarce = fixture.AddProduct(quantity: 1);
        var stockError = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = scarce.Id, Quantity = 2 }]
        }, CancellationToken.None));
        Assert.Equal("insufficient_inventory", stockError.ErrorCode);
        Assert.Equal(1, scarce.AvailableQuantity);
    }

    [Fact]
    public async Task Inactive_customer_cannot_place_an_order()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer(active: false);
        fixture.AddProduct();
        var service = CreateService(fixture);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = fixture.Store.Products[0].Id, Quantity = 1 }]
        }, CancellationToken.None));

        Assert.Equal("inactive_customer", exception.ErrorCode);
        Assert.Empty(fixture.Store.Orders);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_order_or_confirm_and_ship()
    {
        var fixture = new ServiceFixture();
        var owner = fixture.AddCustomer("owner@example.com");
        var product = fixture.AddProduct();
        var service = CreateService(fixture);
        var order = await service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
        }, CancellationToken.None);

        fixture.AddCustomer("other@example.com");
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(order.Id, CancellationToken.None));
        var listed = await service.ListAsync(new OrderQuery(), CancellationToken.None);
        Assert.Empty(listed.Items);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ConfirmAsync(order.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ShipAsync(order.Id, CancellationToken.None));
        Assert.Equal(owner.Id, fixture.Store.Orders.Single().CustomerId);
    }

    [Fact]
    public async Task Cancellation_restores_stock_once_and_shipped_orders_cannot_be_cancelled()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var product = fixture.AddProduct(quantity: 5);
        var service = CreateService(fixture);
        var order = await service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 2 }]
        }, CancellationToken.None);

        var cancelled = await service.CancelAsync(order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Cancelled.ToString(), cancelled.Status);
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal(1, fixture.Products.RestoreCalls);

        var second = await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(order.Id, CancellationToken.None));
        Assert.Equal("invalid_transition", second.ErrorCode);
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal(1, fixture.Products.RestoreCalls);

        var shipped = await PlaceAndShipAsync(fixture, service, product);
        var shipError = await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(shipped.Id, CancellationToken.None));
        Assert.Equal("invalid_transition", shipError.ErrorCode);
        Assert.Equal(4, product.AvailableQuantity);
    }

    [Fact]
    public async Task Confirm_and_ship_do_not_change_inventory_again()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var product = fixture.AddProduct(quantity: 5);
        var service = CreateService(fixture);
        var order = await service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
        }, CancellationToken.None);

        fixture.CurrentUser.IsAdmin = true;
        fixture.CurrentUser.IsCustomer = false;
        await service.ConfirmAsync(order.Id, CancellationToken.None);
        await service.ShipAsync(order.Id, CancellationToken.None);

        Assert.Equal(4, product.AvailableQuantity);
        Assert.Equal(0, fixture.Products.RestoreCalls);
    }

    [Fact]
    public async Task Admin_cannot_place_a_customer_order()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        fixture.CurrentUser.IsCustomer = false;
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = 1, Quantity = 1 }]
        }, CancellationToken.None));
    }

    private static async Task<OrderResponse> PlaceAndShipAsync(ServiceFixture fixture, OrderService service, Product product)
    {
        fixture.CurrentUser.IsAdmin = false;
        fixture.CurrentUser.IsCustomer = true;
        var order = await service.CreateAsync(new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
        }, CancellationToken.None);
        fixture.CurrentUser.IsAdmin = true;
        fixture.CurrentUser.IsCustomer = false;
        await service.ConfirmAsync(order.Id, CancellationToken.None);
        return await service.ShipAsync(order.Id, CancellationToken.None);
    }

    private static OrderService CreateService(ServiceFixture fixture) =>
        new(
            fixture.Orders,
            fixture.Products,
            fixture.Customers,
            fixture.CurrentUser,
            fixture.UnitOfWork,
            fixture.Clock,
            NullLogger<OrderService>.Instance);
}
