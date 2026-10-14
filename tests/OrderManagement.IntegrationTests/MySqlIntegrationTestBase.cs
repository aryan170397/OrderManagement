using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using OrderManagement.Infrastructure.Persistence;
using OrderManagement.Infrastructure.Persistence.Seed;

namespace OrderManagement.IntegrationTests;

public sealed class RequiresMySqlFactAttribute : FactAttribute
{
    public RequiresMySqlFactAttribute()
    {
        if (!MySqlTestGate.IsAvailable)
        {
            Skip = MySqlTestGate.SkipReason;
        }
    }
}

public static class MySqlTestGate
{
    private static readonly Lazy<ProbeResult> Probe = new(Check);

    public static bool IsAvailable => Probe.Value.Available;

    public static string SkipReason => Probe.Value.Reason;

    public static string ServerConnectionString => Probe.Value.ConnectionString;

    private static ProbeResult Check()
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDERMANAGEMENT_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=127.0.0.1;Port=3306;User ID=root;Password=;SslMode=Disabled;Connection Timeout=3;";
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            return new ProbeResult(true, string.Empty, connectionString);
        }
        catch (Exception exception)
        {
            var detail = exception.Message;
            if (detail.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("pwd=", StringComparison.OrdinalIgnoreCase))
            {
                detail = exception.GetType().Name;
            }

            return new ProbeResult(
                false,
                "MySQL is not reachable. Start MySQL 8+ and set ORDERMANAGEMENT_TEST_CONNECTION to a server connection string. " + detail,
                connectionString);
        }
    }

    private sealed record ProbeResult(bool Available, string Reason, string ConnectionString);
}

public sealed class MySqlFixture : IAsyncLifetime
{
    public const string JwtKey = "integration-test-signing-key-32b";
    public const string JwtIssuer = "OrderManagement.Tests";
    public const string JwtAudience = "OrderManagement.Tests";
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "AdminPass123";

    private string? _databaseName;

    public WebApplicationFactory<Program>? Factory { get; private set; }

    public async Task InitializeAsync()
    {
        if (!MySqlTestGate.IsAvailable)
        {
            return;
        }

        var server = new MySqlConnectionStringBuilder(MySqlTestGate.ServerConnectionString)
        {
            Database = string.Empty
        };
        _databaseName = "omtest" + Guid.NewGuid().ToString("N");

        await using (var connection = new MySqlConnection(server.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE `{_databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            await command.ExecuteNonQueryAsync();
        }

        server.Database = _databaseName;
        var appConnection = server.ConnectionString;

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", appConnection);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("AdminSeed__ApplyOnStartup", "false");
        Environment.SetEnvironmentVariable("AdminSeed__Email", AdminEmail);
        Environment.SetEnvironmentVariable("AdminSeed__Password", AdminPassword);
        Environment.SetEnvironmentVariable("AdminSeed__Name", "Test Admin");

        Factory = new OrderApiFactory(appConnection);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await AdminSeeder.SeedAsync(scope.ServiceProvider, CancellationToken.None);
    }

    public async Task ResetAsync()
    {
        if (Factory is null)
        {
            return;
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            foreach (var sql in new[]
                     {
                         "SET FOREIGN_KEY_CHECKS = 0",
                         "TRUNCATE TABLE OrderItems",
                         "TRUNCATE TABLE Orders",
                         "TRUNCATE TABLE Products",
                         "TRUNCATE TABLE Customers",
                         "SET FOREIGN_KEY_CHECKS = 1"
                     })
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        db.ChangeTracker.Clear();
        await AdminSeeder.SeedAsync(scope.ServiceProvider, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (string.IsNullOrEmpty(_databaseName) || !MySqlTestGate.IsAvailable)
        {
            return;
        }

        var server = new MySqlConnectionStringBuilder(MySqlTestGate.ServerConnectionString)
        {
            Database = string.Empty
        };
        await using var connection = new MySqlConnection(server.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`";
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class OrderApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Issuer"] = MySqlFixture.JwtIssuer,
                ["Jwt:Audience"] = MySqlFixture.JwtAudience,
                ["Jwt:SigningKey"] = MySqlFixture.JwtKey,
                ["Jwt:ExpirationMinutes"] = "60",
                ["AdminSeed:ApplyOnStartup"] = "false",
                ["AdminSeed:Email"] = MySqlFixture.AdminEmail,
                ["AdminSeed:Password"] = MySqlFixture.AdminPassword,
                ["AdminSeed:Name"] = "Test Admin"
            });
        });
    }
}

[CollectionDefinition("mysql")]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>;

[Collection("mysql")]
public abstract class MySqlIntegrationTest(MySqlFixture fixture) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected MySqlFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, string? token = null)
    {
        var client = Fixture.Factory!.CreateClient();
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    protected async Task<AuthPayload> RegisterAsync(string? email = null, string password = "Password123")
    {
        email ??= $"user{Guid.NewGuid():N}@example.com";
        var response = await SendAsync(HttpMethod.Post, "/api/auth/register", new
        {
            name = "Test Customer",
            email,
            password,
            role = "Admin"
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, body);
        var payload = JsonSerializer.Deserialize<AuthPayload>(body, JsonOptions)!;
        payload.Password = password;
        payload.Email = email;
        return payload;
    }

    protected async Task<string> LoginAsync(string email, string password)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<AuthPayload>(body, JsonOptions)!.AccessToken;
    }

    protected async Task<string> AdminTokenAsync() =>
        await LoginAsync(MySqlFixture.AdminEmail, MySqlFixture.AdminPassword);

    protected async Task<ProductPayload> CreateProductAsync(string token, string name = "Keyboard", decimal price = 12.50m, int quantity = 10)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/products", new
        {
            name,
            description = "Test product",
            price,
            availableQuantity = quantity
        }, token);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created, body);
        return JsonSerializer.Deserialize<ProductPayload>(body, JsonOptions)!;
    }

    protected static string ExpiredToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(MySqlFixture.JwtKey));
        var token = new JwtSecurityToken(
            MySqlFixture.JwtIssuer,
            MySqlFixture.JwtAudience,
            [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Customer")],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}

public sealed class AuthPayload
{
    public string AccessToken { get; set; } = string.Empty;

    public CustomerPayload Customer { get; set; } = new();

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class CustomerPayload
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed class ProductPayload
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int AvailableQuantity { get; set; }

    public bool IsActive { get; set; }

    public long Version { get; set; }
}

public sealed class OrderPayload
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<OrderItemPayload> Items { get; set; } = [];
}

public sealed class OrderItemPayload
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class PagedProducts
{
    public List<ProductPayload> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}

public sealed class PagedOrders
{
    public List<OrderPayload> Items { get; set; } = [];

    public int TotalCount { get; set; }
}
