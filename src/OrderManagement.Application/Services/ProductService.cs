using Microsoft.Extensions.Logging;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Mapping;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Services;

public sealed class ProductService(
    IProductRepository products,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        if (request.Price <= 0)
        {
            throw new ConflictException("Price must be greater than zero.", "invalid_price");
        }

        if (request.AvailableQuantity < 0)
        {
            throw new ConflictException("Available quantity cannot be negative.", "invalid_inventory");
        }

        var now = clock.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = NormalizeDescription(request.Description),
            Price = request.Price,
            AvailableQuantity = request.AvailableQuantity,
            IsActive = true,
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Product {ProductId} created with quantity {AvailableQuantity}.",
            product.Id,
            product.AvailableQuantity);
        return ResponseMapper.ToResponse(product);
    }

    public async Task<PagedResponse<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var page = await products.ListAsync(query, cancellationToken);
        return ResponseMapper.ToPagedResponse(page, ResponseMapper.ToResponse);
    }

    public async Task<ProductResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Product", id);
        }

        return ResponseMapper.ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var updated = await products.TryUpdateDetailsAsync(
            id,
            request.Name.Trim(),
            NormalizeDescription(request.Description),
            request.Version,
            clock.UtcNow,
            cancellationToken);

        if (!updated)
        {
            return await ThrowMissingOrConflictAsync(id, cancellationToken);
        }

        logger.LogInformation("Product {ProductId} details updated.", id);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProductResponse> UpdatePriceAsync(int id, UpdateProductPriceRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        if (request.Price <= 0)
        {
            throw new ConflictException("Price must be greater than zero.", "invalid_price");
        }

        var updated = await products.TryUpdatePriceAsync(id, request.Price, request.Version, clock.UtcNow, cancellationToken);
        if (!updated)
        {
            return await ThrowMissingOrConflictAsync(id, cancellationToken);
        }

        logger.LogInformation("Product {ProductId} price updated.", id);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProductResponse> UpdateInventoryAsync(int id, UpdateInventoryRequest request, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        if (request.AvailableQuantity < 0)
        {
            throw new ConflictException("Available quantity cannot be negative.", "invalid_inventory");
        }

        var updated = await products.TrySetInventoryAsync(
            id,
            request.AvailableQuantity,
            request.Version,
            clock.UtcNow,
            cancellationToken);

        if (!updated)
        {
            return await ThrowMissingOrConflictAsync(id, cancellationToken);
        }

        logger.LogInformation(
            "Product {ProductId} inventory set to {AvailableQuantity}.",
            id,
            request.AvailableQuantity);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProductResponse> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var updated = await products.TrySetActiveAsync(id, isActive, clock.UtcNow, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException("Product", id);
        }

        logger.LogInformation("Product {ProductId} active state set to {IsActive}.", id, isActive);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<ProductResponse> ThrowMissingOrConflictAsync(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Product", id);
        }

        throw new ConflictException(
            "The product was modified by another request. Refresh and retry.",
            "concurrency_conflict");
    }

    private void EnsureAdmin()
    {
        if (!currentUser.IsAdmin)
        {
            throw new ForbiddenException("Only administrators can manage products.");
        }
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return description.Trim();
    }
}
