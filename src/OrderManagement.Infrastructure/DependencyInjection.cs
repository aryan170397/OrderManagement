using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySql.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Interfaces;
using OrderManagement.Infrastructure.Authentication;
using OrderManagement.Infrastructure.Persistence;
using OrderManagement.Infrastructure.Repositories;

namespace OrderManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySQL(connectionString).AddInterceptors(new UtcSessionInterceptor()));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.Issuer) &&
                    !string.IsNullOrWhiteSpace(options.Audience) &&
                    EncodingHasMinimumLength(options.SigningKey) &&
                    options.ExpirationMinutes is >= 5 and <= 1440,
                "Jwt:Issuer, Jwt:Audience, and a Jwt:SigningKey of at least 32 characters are required. ExpirationMinutes must be between 5 and 1440.")
            .ValidateOnStart();

        services.AddSingleton<ICustomerPasswordHasher, CustomerPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    private static bool EncodingHasMinimumLength(string? signingKey) =>
        !string.IsNullOrWhiteSpace(signingKey) && System.Text.Encoding.UTF8.GetByteCount(signingKey) >= 32;
}
