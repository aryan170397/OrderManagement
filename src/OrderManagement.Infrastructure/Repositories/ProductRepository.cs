using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Repositories;

/// <summary>
/// Inventory mutations use parameterized conditional updates. A read-then-write through the change tracker
/// would allow two transactions to reserve the same unit. The UPDATE holds the InnoDB row lock until commit,
/// and a zero row count means the stock or version check failed.
/// </summary>
public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<PagedResult<Product>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = EscapeLike(query.Search.Trim());
            products = products.Where(product => EF.Functions.Like(product.Name, $"%{term}%"));
        }

        if (query.IsActive is bool isActive)
        {
            products = products.Where(product => product.IsActive == isActive);
        }

        var sortBy = query.SortBy.Trim().ToLowerInvariant();
        var descending = query.SortDirection.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);
        products = (sortBy, descending) switch
        {
            ("price", true) => products.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            ("price", false) => products.OrderBy(product => product.Price).ThenBy(product => product.Id),
            ("createdat", true) => products.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.Id),
            ("createdat", false) => products.OrderBy(product => product.CreatedAt).ThenBy(product => product.Id),
            (_, true) => products.OrderByDescending(product => product.Name).ThenByDescending(product => product.Id),
            _ => products.OrderBy(product => product.Name).ThenBy(product => product.Id)
        };

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Add(product);
        return Task.CompletedTask;
    }

    public async Task<bool> TryUpdateDetailsAsync(
        int id,
        string name,
        string? description,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET Name = {name},
                Description = {description},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {id} AND Version = {version}
            """,
            cancellationToken);
        return rows == 1;
    }

    public async Task<bool> TryUpdatePriceAsync(
        int id,
        decimal price,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET Price = {price},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {id} AND Version = {version}
            """,
            cancellationToken);
        return rows == 1;
    }

    public async Task<bool> TrySetInventoryAsync(
        int id,
        int availableQuantity,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET AvailableQuantity = {availableQuantity},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {id} AND Version = {version}
            """,
            cancellationToken);
        return rows == 1;
    }

    public async Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var active = isActive ? 1 : 0;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET IsActive = {active},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {id}
            """,
            cancellationToken);
        return rows == 1;
    }

    public async Task<InventoryReservationResult> TryReserveAsync(
        int productId,
        int quantity,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET AvailableQuantity = AvailableQuantity - {quantity},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {productId}
              AND IsActive = 1
              AND AvailableQuantity >= {quantity}
            """,
            cancellationToken);

        if (rows == 1)
        {
            return InventoryReservationResult.Success();
        }

        var product = await db.Products.AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => new { item.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return InventoryReservationResult.Fail(InventoryReservationFailure.NotFound);
        }

        if (!product.IsActive)
        {
            return InventoryReservationResult.Fail(InventoryReservationFailure.Inactive);
        }

        return InventoryReservationResult.Fail(InventoryReservationFailure.InsufficientStock);
    }

    public async Task RestoreAsync(int productId, int quantity, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var maximumBeforeAdd = int.MaxValue - quantity;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Products
            SET AvailableQuantity = AvailableQuantity + {quantity},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)},
                Version = Version + 1
            WHERE Id = {productId}
              AND AvailableQuantity <= {maximumBeforeAdd}
            """,
            cancellationToken);

        if (rows != 1)
        {
            throw new ConflictException(
                $"Reserved inventory for product {productId} could not be restored.",
                "inventory_restore_failed");
        }
    }

    public async Task<(decimal Price, string Name)?> GetPriceAndNameAsync(int productId, CancellationToken cancellationToken)
    {
        var snapshot = await db.Products.AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new { product.Price, product.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return snapshot is null ? null : (snapshot.Price, snapshot.Name);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
