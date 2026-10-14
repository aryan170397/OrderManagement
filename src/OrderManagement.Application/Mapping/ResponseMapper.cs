using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Mapping;

public static class ResponseMapper
{
    public static CustomerResponse ToResponse(Customer customer) =>
        new()
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email,
            Role = customer.Role.ToString(),
            IsActive = customer.IsActive,
            CreatedAtUtc = DateTime.SpecifyKind(customer.CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(customer.UpdatedAt, DateTimeKind.Utc)
        };

    public static ProductResponse ToResponse(Product product) =>
        new()
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            AvailableQuantity = product.AvailableQuantity,
            IsActive = product.IsActive,
            Version = product.Version,
            CreatedAtUtc = DateTime.SpecifyKind(product.CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(product.UpdatedAt, DateTimeKind.Utc)
        };

    public static OrderResponse ToResponse(Order order) =>
        new()
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            CreatedAtUtc = DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(order.UpdatedAt, DateTimeKind.Utc),
            Items = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                })
                .ToList()
        };

    public static PagedResponse<TResponse> ToPagedResponse<TEntity, TResponse>(
        PagedResult<TEntity> page,
        Func<TEntity, TResponse> map) =>
        new()
        {
            Items = page.Items.Select(map).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages
        };
}
