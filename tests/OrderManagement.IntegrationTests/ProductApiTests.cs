using System.Net;
using System.Text.Json;

namespace OrderManagement.IntegrationTests;

public sealed class ProductApiTests(MySqlFixture fixture) : MySqlIntegrationTest(fixture)
{
    [RequiresMySqlFact]
    public async Task Admin_can_manage_price_inventory_and_activation()
    {
        var admin = await AdminTokenAsync();
        var created = await CreateProductAsync(admin, "Desk lamp", 19.99m, 4);
        Assert.True(created.IsActive);
        Assert.Equal(4, created.AvailableQuantity);

        var price = await SendAsync(HttpMethod.Patch, $"/api/products/{created.Id}/price", new
        {
            price = 21.50m,
            version = created.Version
        }, admin);
        var priced = JsonSerializer.Deserialize<ProductPayload>(await price.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(HttpStatusCode.OK, price.StatusCode);
        Assert.Equal(21.50m, priced.Price);

        var inventory = await SendAsync(HttpMethod.Patch, $"/api/products/{created.Id}/inventory", new
        {
            availableQuantity = 8,
            version = priced.Version
        }, admin);
        var stocked = JsonSerializer.Deserialize<ProductPayload>(await inventory.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(8, stocked.AvailableQuantity);

        var stale = await SendAsync(HttpMethod.Patch, $"/api/products/{created.Id}/inventory", new
        {
            availableQuantity = 1,
            version = created.Version
        }, admin);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var deactivated = await SendAsync(HttpMethod.Patch, $"/api/products/{created.Id}/deactivate", token: admin);
        var inactive = JsonSerializer.Deserialize<ProductPayload>(await deactivated.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.False(inactive.IsActive);

        var activated = await SendAsync(HttpMethod.Patch, $"/api/products/{created.Id}/activate", token: admin);
        Assert.True(JsonSerializer.Deserialize<ProductPayload>(await activated.Content.ReadAsStringAsync(), JsonOptions)!.IsActive);
    }

    [RequiresMySqlFact]
    public async Task Listing_is_paged_filtered_and_rejects_unknown_sort_fields()
    {
        var admin = await AdminTokenAsync();
        var customer = await RegisterAsync();
        await CreateProductAsync(admin, "Alpha");
        await CreateProductAsync(admin, "Beta");
        await CreateProductAsync(admin, "Alpine");

        var page = await SendAsync(HttpMethod.Get, "/api/products?page=1&pageSize=2&sortBy=name&sortDirection=asc", token: customer.AccessToken);
        var paged = JsonSerializer.Deserialize<PagedProducts>(await page.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(2, paged.Items.Count);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.TotalPages);

        var search = await SendAsync(HttpMethod.Get, "/api/products?search=Alp", token: customer.AccessToken);
        var found = JsonSerializer.Deserialize<PagedProducts>(await search.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(2, found.TotalCount);

        var invalidSort = await SendAsync(HttpMethod.Get, "/api/products?sortBy=name;%20drop%20table", token: customer.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);

        var missing = await SendAsync(HttpMethod.Get, "/api/products/99999", token: customer.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var badPrice = await SendAsync(HttpMethod.Post, "/api/products", new
        {
            name = "Bad",
            price = -1,
            availableQuantity = 1
        }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, badPrice.StatusCode);
    }
}
