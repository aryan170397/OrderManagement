using OrderManagement.Application.Common;
using OrderManagement.Application.DTOs;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Interfaces;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<PagedResult<Order>> ListAsync(OrderQuery query, int? customerId, CancellationToken cancellationToken);

    Task<bool> TryTransitionAsync(
        int id,
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);
}
