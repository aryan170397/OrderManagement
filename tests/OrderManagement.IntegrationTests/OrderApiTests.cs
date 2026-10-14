using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.IntegrationTests;

public sealed class OrderApiTests(MySqlFixture fixture) : MySqlIntegrationTest(fixture)
{
    [RequiresMySqlFact]
    public async Task Order_persists_server_calculated_prices_and_follows_the_lifecycle()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var keyboard = await CreateProductAsync(admin, "Keyboard", 12.50m, 10);
        var mouse = await CreateProductAsync(admin, "Mouse", 3.00m, 5);

        var created = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[]
            {
                new { productId = keyboard.Id, quantity = 2 },
                new { productId = mouse.Id, quantity = 1 }
            }
        }, customer.AccessToken);
        var body = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = JsonSerializer.Deserialize<OrderPayload>(body, JsonOptions)!;
        Assert.Equal("Pending", order.Status);
        Assert.Equal(28.00m, order.TotalAmount);
        Assert.Equal(12.50m, order.Items.Single(item => item.ProductId == keyboard.Id).UnitPrice);
        Assert.Equal(25.00m, order.Items.Single(item => item.ProductId == keyboard.Id).LineTotal);

        await using (var scope = Fixture.Factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == order.Id);
            Assert.Equal(28.00m, stored.TotalAmount);
            Assert.Equal("Pending", stored.Status.ToString());
            var keyboardRow = await db.Products.SingleAsync(product => product.Id == keyboard.Id);
            Assert.Equal(8, keyboardRow.AvailableQuantity);
        }

        var priceChange = await SendAsync(HttpMethod.Patch, $"/api/products/{keyboard.Id}/price", new
        {
            price = 99m,
            version = (await ReloadProductAsync(keyboard.Id)).Version
        }, admin);
        Assert.Equal(HttpStatusCode.OK, priceChange.StatusCode);

        var reread = await SendAsync(HttpMethod.Get, $"/api/orders/{order.Id}", token: customer.AccessToken);
        var historical = JsonSerializer.Deserialize<OrderPayload>(await reread.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(12.50m, historical.Items.Single(item => item.ProductId == keyboard.Id).UnitPrice);

        var confirm = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/confirm", token: admin);
        Assert.Equal("Confirmed", JsonSerializer.Deserialize<OrderPayload>(await confirm.Content.ReadAsStringAsync(), JsonOptions)!.Status);
        var ship = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/ship", token: admin);
        Assert.Equal("Shipped", JsonSerializer.Deserialize<OrderPayload>(await ship.Content.ReadAsStringAsync(), JsonOptions)!.Status);

        var cancelShipped = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/cancel", token: admin);
        Assert.Equal(HttpStatusCode.Conflict, cancelShipped.StatusCode);
        Assert.Equal(8, (await ReloadProductAsync(keyboard.Id)).AvailableQuantity);

        var customerConfirm = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/confirm", token: customer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, customerConfirm.StatusCode);
    }

    [RequiresMySqlFact]
    public async Task Insufficient_stock_rolls_back_every_reservation()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var available = await CreateProductAsync(admin, "Available", 10m, 5);
        var empty = await CreateProductAsync(admin, "Empty", 10m, 0);

        var response = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[]
            {
                new { productId = available.Id, quantity = 2 },
                new { productId = empty.Id, quantity = 1 }
            }
        }, customer.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("insufficient_inventory", (await ReadJsonAsync(response)).GetProperty("errorCode").GetString());

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, (await db.Products.SingleAsync(product => product.Id == available.Id)).AvailableQuantity);
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [RequiresMySqlFact]
    public async Task Cancellation_restores_inventory_once_and_customers_cannot_read_other_orders()
    {
        var admin = await AdminTokenAsync();
        var owner = await RegisterAsync();
        var other = await RegisterAsync();
        var product = await CreateProductAsync(admin, "Cable", 4m, 6);
        var created = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 2 } }
        }, owner.AccessToken);
        var order = JsonSerializer.Deserialize<OrderPayload>(await created.Content.ReadAsStringAsync(), JsonOptions)!;

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/orders/{order.Id}", token: other.AccessToken)).StatusCode);
        var otherList = JsonSerializer.Deserialize<PagedOrders>(
            await (await SendAsync(HttpMethod.Get, "/api/orders", token: other.AccessToken)).Content.ReadAsStringAsync(),
            JsonOptions)!;
        Assert.Equal(0, otherList.TotalCount);

        var adminList = JsonSerializer.Deserialize<PagedOrders>(
            await (await SendAsync(HttpMethod.Get, "/api/orders?status=Pending", token: admin)).Content.ReadAsStringAsync(),
            JsonOptions)!;
        Assert.Equal(1, adminList.TotalCount);

        var cancelled = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/cancel", token: owner.AccessToken);
        Assert.Equal("Cancelled", JsonSerializer.Deserialize<OrderPayload>(await cancelled.Content.ReadAsStringAsync(), JsonOptions)!.Status);
        Assert.Equal(6, (await ReloadProductAsync(product.Id)).AvailableQuantity);

        var again = await SendAsync(HttpMethod.Patch, $"/api/orders/{order.Id}/cancel", token: owner.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(6, (await ReloadProductAsync(product.Id)).AvailableQuantity);
    }

    [RequiresMySqlFact]
    public async Task Inactive_customer_and_inactive_product_cannot_be_purchased()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var product = await CreateProductAsync(admin, "Lamp", 8m, 3);
        await SendAsync(HttpMethod.Patch, $"/api/products/{product.Id}/deactivate", token: admin);

        var inactiveProduct = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } }
        }, customer.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, inactiveProduct.StatusCode);
        Assert.Equal("inactive_product", (await ReadJsonAsync(inactiveProduct)).GetProperty("errorCode").GetString());

        await SendAsync(HttpMethod.Patch, $"/api/products/{product.Id}/activate", token: admin);
        await SendAsync(HttpMethod.Patch, $"/api/customers/{customer.Customer.Id}/deactivate", token: admin);
        var inactiveCustomer = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } }
        }, customer.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, inactiveCustomer.StatusCode);
        Assert.Equal("inactive_customer", (await ReadJsonAsync(inactiveCustomer)).GetProperty("errorCode").GetString());
        Assert.Equal(3, (await ReloadProductAsync(product.Id)).AvailableQuantity);
    }

    [RequiresMySqlFact]
    public async Task Foreign_key_and_check_constraints_protect_history_and_inventory()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        var product = await CreateProductAsync(admin, "History", 5m, 2);
        var response = await SendAsync(HttpMethod.Post, "/api/orders", new
        {
            items = new[] { new { productId = product.Id, quantity = 1 } }
        }, customer.AccessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Products.SingleAsync(item => item.Id == product.Id);
        db.Products.Remove(entity);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Products SET AvailableQuantity = -1 WHERE Id = {product.Id}"));
    }

    private async Task<ProductPayload> ReloadProductAsync(int id)
    {
        var admin = await AdminTokenAsync();
        var response = await SendAsync(HttpMethod.Get, $"/api/products/{id}", token: admin);
        return JsonSerializer.Deserialize<ProductPayload>(await response.Content.ReadAsStringAsync(), JsonOptions)!;
    }
}
