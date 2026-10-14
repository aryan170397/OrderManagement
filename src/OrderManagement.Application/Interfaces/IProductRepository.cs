using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<PagedResult<Product>> ListAsync(ProductQuery query, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task<bool> TryUpdateDetailsAsync(
        int id,
        string name,
        string? description,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryUpdatePriceAsync(
        int id,
        decimal price,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TrySetInventoryAsync(
        int id,
        int availableQuantity,
        long version,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken);

    Task<InventoryReservationResult> TryReserveAsync(
        int productId,
        int quantity,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task RestoreAsync(int productId, int quantity, DateTime updatedAtUtc, CancellationToken cancellationToken);

    Task<(decimal Price, string Name)?> GetPriceAndNameAsync(int productId, CancellationToken cancellationToken);
}
