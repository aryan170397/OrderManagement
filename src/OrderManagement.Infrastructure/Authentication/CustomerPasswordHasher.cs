using Microsoft.AspNetCore.Identity;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Authentication;

public sealed class CustomerPasswordHasher : ICustomerPasswordHasher
{
    private readonly PasswordHasher<Customer> _hasher = new();

    public string HashPassword(Customer customer, string password) =>
        _hasher.HashPassword(customer, password);

    public bool VerifyPassword(Customer customer, string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(customer, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
