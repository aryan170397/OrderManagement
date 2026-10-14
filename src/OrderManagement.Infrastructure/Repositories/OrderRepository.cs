using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Repositories;

public sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        db.Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<PagedResult<Order>> ListAsync(OrderQuery query, int? customerId, CancellationToken cancellationToken)
    {
        var filtered = db.Orders.AsNoTracking().AsQueryable();
        if (customerId is int ownerId)
        {
            filtered = filtered.Where(order => order.CustomerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
        {
            filtered = filtered.Where(order => order.Status == status);
        }

        if (query.CreatedFromUtc is DateTime from)
        {
            filtered = filtered.Where(order => order.CreatedAt >= from);
        }

        if (query.CreatedToUtc is DateTime to)
        {
            filtered = filtered.Where(order => order.CreatedAt <= to);
        }

        var total = await filtered.CountAsync(cancellationToken);
        var pageIds = await filtered
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

        var orders = await db.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => pageIds.Contains(order.Id))
            .ToListAsync(cancellationToken);

        var ordered = pageIds
            .Select(id => orders.First(order => order.Id == id))
            .ToList();

        return new PagedResult<Order>
        {
            Items = ordered,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    public async Task<bool> TryTransitionAsync(
        int id,
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var expected = expectedStatus.ToString();
        var next = newStatus.ToString();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Orders
            SET Status = {next},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)}
            WHERE Id = {id} AND Status = {expected}
            """,
            cancellationToken);
        return rows == 1;
    }
}
