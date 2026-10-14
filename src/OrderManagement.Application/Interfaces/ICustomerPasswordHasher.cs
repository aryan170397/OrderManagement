using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Interfaces;

public interface ICustomerPasswordHasher
{
    string HashPassword(Customer customer, string password);

    bool VerifyPassword(Customer customer, string passwordHash, string password);
}
