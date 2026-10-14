using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.IntegrationTests;

public sealed class AuthApiTests(MySqlFixture fixture) : MySqlIntegrationTest(fixture)
{
    [RequiresMySqlFact]
    public async Task Register_and_login_return_a_customer_token_without_secrets()
    {
        var registered = await RegisterAsync();
        Assert.Equal("Customer", registered.Customer.Role);
        Assert.True(registered.Customer.IsActive);

        var login = await SendAsync(HttpMethod.Post, "/api/auth/login", new
        {
            email = registered.Email.ToUpperInvariant(),
            password = registered.Password
        });
        var body = await login.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(registered.Password, body, StringComparison.Ordinal);

        await using var scope = Fixture.Factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Customers.SingleAsync(customer => customer.Id == registered.Customer.Id);
        Assert.NotEqual(registered.Password, stored.PasswordHash);
        Assert.StartsWith("AQAAAA", stored.PasswordHash, StringComparison.Ordinal);
    }

    [RequiresMySqlFact]
    public async Task Duplicate_email_and_bad_credentials_are_rejected()
    {
        var registered = await RegisterAsync("ada@example.com");
        var duplicate = await SendAsync(HttpMethod.Post, "/api/auth/register", new
        {
            name = "Ada",
            email = "Ada@example.com",
            password = "Password123"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("duplicate_email", (await ReadJsonAsync(duplicate)).GetProperty("errorCode").GetString());

        var invalid = await SendAsync(HttpMethod.Post, "/api/auth/login", new
        {
            email = registered.Email,
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [RequiresMySqlFact]
    public async Task Protected_routes_reject_missing_invalid_and_expired_tokens()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/products", token: "not-a-jwt")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/products", token: ExpiredToken())).StatusCode);

        var customer = await RegisterAsync();
        var forbidden = await SendAsync(HttpMethod.Post, "/api/products", new
        {
            name = "Desk",
            price = 10m,
            availableQuantity = 1
        }, customer.AccessToken);
        var body = await forbidden.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySqlException", body, StringComparison.Ordinal);
    }

    [RequiresMySqlFact]
    public async Task Customer_can_read_and_update_only_their_own_profile()
    {
        var customer = await RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, "/api/customers/me", token: customer.AccessToken)).StatusCode);

        var updated = await SendAsync(HttpMethod.Put, "/api/customers/me", new
        {
            name = "Updated Name",
            role = "Admin",
            email = "hacker@example.com"
        }, customer.AccessToken);
        var profile = JsonSerializer.Deserialize<CustomerPayload>(await updated.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Updated Name", profile.Name);
        Assert.Equal("Customer", profile.Role);
        Assert.Equal(customer.Email, profile.Email);
    }
}
