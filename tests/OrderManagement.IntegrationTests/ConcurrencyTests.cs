using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.IntegrationTests;

public sealed class ConcurrencyTests(MySqlFixture fixture) : MySqlIntegrationTest(fixture)
{
    [RequiresMySqlFact]
    public async Task Concurrent_purchases_cannot_oversell_the_last_units()
    {
        var admin = await AdminTokenAsync();
        var product = await CreateProductAsync(admin, "Last units", 9m, 5);
        var buyers = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RegisterAsync()));

        var responses = await Task.WhenAll(buyers.Select(buyer => SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } }
        }, buyer.AccessToken)));

        var success = responses.Count(response => response.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(5, success);
        Assert.Equal(5, conflicts);

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Products.SingleAsync(item => item.Id == product.Id);
        Assert.Equal(0, stored.AvailableQuantity);
        Assert.Equal(5, await db.Orders.CountAsync());
        Assert.True(stored.AvailableQuantity >= 0);
    }

    [RequiresMySqlFact]
    public async Task Concurrent_cancellation_restores_inventory_once()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var product = await CreateProductAsync(admin, "Restore once", 2m, 10);
        var created = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 3 } }
        }, customer.AccessToken);
        var order = JsonSerializer.Deserialize<OrderPayload>(await created.Content.ReadAsStringAsync(), JsonOptions)!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/cancel", token: customer.AccessToken)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10, (await db.Products.SingleAsync(item => item.Id == product.Id)).AvailableQuantity);
        Assert.Equal("Cancelled", (await db.Orders.SingleAsync()).Status.ToString());
    }

    [RequiresMySqlFact]
    public async Task Concurrent_ship_and_cancel_leave_one_consistent_outcome()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var product = await CreateProductAsync(admin, "Race", 6m, 4);
        var created = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } }
        }, customer.AccessToken);
        var order = JsonSerializer.Deserialize<OrderPayload>(await created.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/confirm", token: admin)).StatusCode);

        var ship = SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/ship", token: admin);
        var cancel = SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/cancel", token: admin);
        var responses = await Task.WhenAll(ship, cancel);
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedOrder = await db.Orders.SingleAsync();
        var storedProduct = await db.Products.SingleAsync(item => item.Id == product.Id);
        if (storedOrder.Status.ToString() == "Shipped")
        {
            Assert.Equal(3, storedProduct.AvailableQuantity);
        }
        else
        {
            Assert.Equal("Cancelled", storedOrder.Status.ToString());
            Assert.Equal(4, storedProduct.AvailableQuantity);
        }

        Assert.True(storedProduct.AvailableQuantity >= 0);
    }
}
