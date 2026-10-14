using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Repositories;

public sealed class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().FirstOrDefaultAsync(customer => customer.Email == normalizedEmail, cancellationToken);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Customers.Add(customer);
        return Task.CompletedTask;
    }

    public async Task<bool> TryUpdateNameAsync(int id, string name, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Customers
            SET Name = {name},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)}
            WHERE Id = {id}
            """,
            cancellationToken);
        return rows == 1;
    }

    public async Task<bool> TrySetActiveAsync(int id, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        var active = isActive ? 1 : 0;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE Customers
            SET IsActive = {active},
                UpdatedAt = {DateTimeStorage.ToUnspecifiedUtc(updatedAtUtc)}
            WHERE Id = {id}
            """,
            cancellationToken);
        return rows == 1;
    }
}
