using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Interfaces;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Rules;

namespace OrderManagement.Infrastructure.Persistence.Seed;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");
        var configuration = services.GetRequiredService<IConfiguration>();
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        var name = configuration["AdminSeed:Name"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Administrator seed skipped because AdminSeed:Email or AdminSeed:Password is not configured.");
            return;
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<ICustomerPasswordHasher>();
        var existing = await db.Customers.FirstOrDefaultAsync(customer => customer.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Admin)
            {
                logger.LogError(
                    "Administrator seed was not applied because the configured email is already used by a non-admin account.");
            }

            return;
        }

        var now = DateTime.UtcNow;
        var admin = new Customer
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Administrator" : name.Trim(),
            Email = normalizedEmail,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            PasswordHash = "pending"
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Customers.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Administrator account provisioned.");
    }
}
