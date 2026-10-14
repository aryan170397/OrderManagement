using Microsoft.Extensions.Logging.Abstractions;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Services;
using OrderManagement.UnitTests.Support;

namespace OrderManagement.UnitTests.Services;

public class ProductServiceTests
{
    [Fact]
    public async Task Admin_can_create_an_active_product()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        fixture.CurrentUser.IsCustomer = false;
        var service = CreateService(fixture);

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "  Desk lamp ",
            Description = " Warm light ",
            Price = 19.99m,
            AvailableQuantity = 4
        }, CancellationToken.None);

        Assert.Equal("Desk lamp", product.Name);
        Assert.Equal("Warm light", product.Description);
        Assert.Equal(19.99m, product.Price);
        Assert.Equal(4, product.AvailableQuantity);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task Invalid_price_and_negative_inventory_are_rejected()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateProductRequest
        {
            Name = "Desk",
            Price = 0,
            AvailableQuantity = 1
        }, CancellationToken.None));

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateProductRequest
        {
            Name = "Desk",
            Price = 10,
            AvailableQuantity = -1
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Customer_cannot_manage_products()
    {
        var fixture = new ServiceFixture();
        fixture.AddCustomer();
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new CreateProductRequest
        {
            Name = "Desk",
            Price = 10,
            AvailableQuantity = 1
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Stale_version_does_not_overwrite_price_or_inventory()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        var product = fixture.AddProduct();
        var service = CreateService(fixture);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.UpdatePriceAsync(product.Id, new UpdateProductPriceRequest
        {
            Price = 50,
            Version = 99
        }, CancellationToken.None));

        Assert.Equal("concurrency_conflict", exception.ErrorCode);
        Assert.Equal(25m, fixture.Store.Products.Single().Price);
    }

    [Fact]
    public async Task Deactivation_marks_the_product_inactive()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        var product = fixture.AddProduct();
        var service = CreateService(fixture);

        var updated = await service.SetActiveAsync(product.Id, false, CancellationToken.None);

        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Missing_product_returns_not_found()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.IsAdmin = true;
        var service = CreateService(fixture);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(42, CancellationToken.None));
    }

    private static ProductService CreateService(ServiceFixture fixture) =>
        new(fixture.Products, fixture.CurrentUser, fixture.UnitOfWork, fixture.Clock, NullLogger<ProductService>.Instance);
}
