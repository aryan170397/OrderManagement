using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.UnitTests.Support;

internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public int UserId { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsCustomer { get; set; } = true;

    public int GetUserId() => UserId;
}

internal sealed class FakePasswordHasher : ICustomerPasswordHasher
{
    public string HashPassword(Customer customer, string password) => "hashed:" + password;

    public bool VerifyPassword(Customer customer, string passwordHash, string password) =>
        passwordHash == "hashed:" + password;
}

internal sealed class FakeTokenService : ITokenService
{
    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(Customer customer) =>
        ("token-" + customer.Id, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
}

internal sealed class ImmediateUnitOfWork : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        action(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class InMemoryStore
{
    private int _customerIds = 1;
    private int _productIds = 1;
    private int _orderIds = 1;
    private int _itemIds = 1;

    public List<Customer> Customers { get; } = [];

    public List<Product> Products { get; } = [];

    public List<Order> Orders { get; } = [];

    public int NextCustomerId() => _customerIds++;

    public int NextProductId() => _productIds++;

    public int NextOrderId() => _orderIds++;

    public int NextItemId() => _itemIds++;
}

internal sealed class FakeCustomerRepository(InMemoryStore store) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(store.Customers.FirstOrDefault(customer => customer.Id == id));

    public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(store.Customers.FirstOrDefault(customer => customer.Email == normalizedEmail));

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        customer.Id = store.NextCustomerId();
        store.Customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<bool> TryUpdateNameAsync(int id, string name, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var customer = store.Customers.FirstOrDefault(item => item.Id == id);
        if (customer is null)
        {
            return Task.FromResult(false);
        }

        customer.Name = name;
        customer.UpdatedAt = updatedAtUtc;
        return Task.FromResult(true);
    }

    public Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var customer = store.Customers.FirstOrDefault(item => item.Id == id);
        if (customer is null)
        {
            return Task.FromResult(false);
        }

        customer.IsActive = isActive;
        customer.UpdatedAt = updatedAtUtc;
        return Task.FromResult(true);
    }
}

internal sealed class FakeProductRepository(InMemoryStore store) : IProductRepository
{
    public int ReserveCalls { get; private set; }

    public int RestoreCalls { get; private set; }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(store.Products.FirstOrDefault(product => product.Id == id));

    public Task<PagedResult<Product>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<Product> products = store.Products;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            products = products.Where(product => product.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsActive is bool isActive)
        {
            products = products.Where(product => product.IsActive == isActive);
        }

        var list = products.ToList();
        return Task.FromResult(new PagedResult<Product>
        {
            Items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = list.Count
        });
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        product.Id = store.NextProductId();
        store.Products.Add(product);
        return Task.CompletedTask;
    }

    public Task<bool> TryUpdateDetailsAsync(int id, string name, string? description, long version, DateTime updatedAtUtc, CancellationToken cancellationToken) =>
        Update(id, version, product =>
        {
            product.Name = name;
            product.Description = description;
            product.UpdatedAt = updatedAtUtc;
        });

    public Task<bool> TryUpdatePriceAsync(int id, decimal price, long version, DateTime updatedAtUtc, CancellationToken cancellationToken) =>
        Update(id, version, product =>
        {
            product.Price = price;
            product.UpdatedAt = updatedAtUtc;
        });

    public Task<bool> TrySetInventoryAsync(int id, int availableQuantity, long version, DateTime updatedAtUtc, CancellationToken cancellationToken) =>
        Update(id, version, product =>
        {
            product.AvailableQuantity = availableQuantity;
            product.UpdatedAt = updatedAtUtc;
        });

    public Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var product = store.Products.FirstOrDefault(item => item.Id == id);
        if (product is null)
        {
            return Task.FromResult(false);
        }

        product.IsActive = isActive;
        product.UpdatedAt = updatedAtUtc;
        product.Version++;
        return Task.FromResult(true);
    }

    public Task<InventoryReservationResult> TryReserveAsync(int productId, int quantity, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        ReserveCalls++;
        var product = store.Products.FirstOrDefault(item => item.Id == productId);
        if (product is null)
        {
            return Task.FromResult(InventoryReservationResult.Fail(InventoryReservationFailure.NotFound));
        }

        if (!product.IsActive)
        {
            return Task.FromResult(InventoryReservationResult.Fail(InventoryReservationFailure.Inactive));
        }

        if (product.AvailableQuantity < quantity)
        {
            return Task.FromResult(InventoryReservationResult.Fail(InventoryReservationFailure.InsufficientStock));
        }

        product.AvailableQuantity -= quantity;
        product.UpdatedAt = updatedAtUtc;
        product.Version++;
        return Task.FromResult(InventoryReservationResult.Success());
    }

    public Task RestoreAsync(int productId, int quantity, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        RestoreCalls++;
        var product = store.Products.FirstOrDefault(item => item.Id == productId)
            ?? throw new ConflictException($"Reserved inventory for product {productId} could not be restored.", "inventory_restore_failed");
        product.AvailableQuantity += quantity;
        product.UpdatedAt = updatedAtUtc;
        product.Version++;
        return Task.CompletedTask;
    }

    public Task<(decimal Price, string Name)?> GetPriceAndNameAsync(int productId, CancellationToken cancellationToken)
    {
        var product = store.Products.FirstOrDefault(item => item.Id == productId);
        return Task.FromResult(product is null ? ((decimal Price, string Name)?)null : (product.Price, product.Name));
    }

    private Task<bool> Update(int id, long version, Action<Product> change)
    {
        var product = store.Products.FirstOrDefault(item => item.Id == id && item.Version == version);
        if (product is null)
        {
            return Task.FromResult(false);
        }

        change(product);
        product.Version++;
        return Task.FromResult(true);
    }
}

internal sealed class FakeOrderRepository(InMemoryStore store) : IOrderRepository
{
    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        order.Id = store.NextOrderId();
        foreach (var item in order.Items)
        {
            item.Id = store.NextItemId();
            item.OrderId = order.Id;
        }

        store.Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(store.Orders.FirstOrDefault(order => order.Id == id));

    public Task<PagedResult<Order>> ListAsync(OrderQuery query, int? customerId, CancellationToken cancellationToken)
    {
        IEnumerable<Order> orders = store.Orders;
        if (customerId is int ownerId)
        {
            orders = orders.Where(order => order.CustomerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<OrderStatus>(query.Status, true, out var status))
        {
            orders = orders.Where(order => order.Status == status);
        }

        var list = orders.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id).ToList();
        return Task.FromResult(new PagedResult<Order>
        {
            Items = list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = list.Count
        });
    }

    public Task<bool> TryTransitionAsync(int id, OrderStatus expectedStatus, OrderStatus newStatus, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var order = store.Orders.FirstOrDefault(item => item.Id == id && item.Status == expectedStatus);
        if (order is null)
        {
            return Task.FromResult(false);
        }

        order.Status = newStatus;
        order.UpdatedAt = updatedAtUtc;
        return Task.FromResult(true);
    }
}

internal sealed class ServiceFixture
{
    public ServiceFixture()
    {
        Store = new InMemoryStore();
        Clock = new FixedClock(new DateTime(2026, 10, 11, 9, 0, 0));
        CurrentUser = new TestCurrentUser { UserId = 1, IsCustomer = true };
        Customers = new FakeCustomerRepository(Store);
        Products = new FakeProductRepository(Store);
        Orders = new FakeOrderRepository(Store);
        UnitOfWork = new ImmediateUnitOfWork();
    }

    public InMemoryStore Store { get; }

    public FixedClock Clock { get; }

    public TestCurrentUser CurrentUser { get; }

    public FakeCustomerRepository Customers { get; }

    public FakeProductRepository Products { get; }

    public FakeOrderRepository Orders { get; }

    public ImmediateUnitOfWork UnitOfWork { get; }

    public Product AddProduct(string name = "Keyboard", decimal price = 25m, int quantity = 10, bool active = true)
    {
        var product = new Product
        {
            Id = Store.NextProductId(),
            Name = name,
            Price = price,
            AvailableQuantity = quantity,
            IsActive = active,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Store.Products.Add(product);
        return product;
    }

    public Customer AddCustomer(string email = "ada@example.com", bool active = true, UserRole role = UserRole.Customer)
    {
        var customer = new Customer
        {
            Id = Store.NextCustomerId(),
            Name = "Ada",
            Email = email,
            PasswordHash = "hashed:Password123",
            Role = role,
            IsActive = active,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow
        };
        Store.Customers.Add(customer);
        CurrentUser.UserId = customer.Id;
        CurrentUser.IsAdmin = role == UserRole.Admin;
        CurrentUser.IsCustomer = role == UserRole.Customer;
        return customer;
    }
}
