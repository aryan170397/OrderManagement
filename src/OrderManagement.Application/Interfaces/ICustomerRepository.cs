using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task<bool> TryUpdateNameAsync(int id, string name, DateTime updatedAtUtc, CancellationToken cancellationToken);

    Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken);
}
