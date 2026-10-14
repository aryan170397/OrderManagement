using Microsoft.Extensions.Logging;
using OrderManagement.Application.DTOs;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;
using OrderManagement.Application.Mapping;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Rules;

namespace OrderManagement.Application.Services;

public sealed class AuthService(
    ICustomerRepository customers,
    ICustomerPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var existing = await customers.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.", "duplicate_email");
        }

        var now = clock.UtcNow;
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Email = email,
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        customer.PasswordHash = passwordHasher.HashPassword(customer, request.Password);

        await customers.AddAsync(customer, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            throw new ConflictException("An account with this email already exists.", "duplicate_email");
        }

        logger.LogInformation("Customer {CustomerId} registered.", customer.Id);
        return CreateAuthResponse(customer);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);
        var customer = await customers.GetByEmailAsync(email, cancellationToken);
        var passwordValid = customer is not null &&
                            passwordHasher.VerifyPassword(customer, customer.PasswordHash, request.Password);

        if (customer is null || !customer.IsActive || !passwordValid)
        {
            logger.LogWarning("Authentication failed.");
            throw new UnauthorizedAppException("Invalid email or password.");
        }

        logger.LogInformation("Customer {CustomerId} authenticated.", customer.Id);
        return CreateAuthResponse(customer);
    }

    private AuthResponse CreateAuthResponse(Customer customer)
    {
        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(customer);
        return new AuthResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            Customer = ResponseMapper.ToResponse(customer)
        };
    }
}
