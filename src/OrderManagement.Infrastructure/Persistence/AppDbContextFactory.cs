using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MySql.EntityFrameworkCore.Extensions;

namespace OrderManagement.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var current = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(current)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile(Path.Combine(current, "src", "OrderManagement.Api", "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) ||
            connectionString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "Server=localhost;Port=3306;Database=ordermanagement;User ID=ordermanagement;Password=changeme;SslMode=Preferred;CharSet=utf8mb4;";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseMySQL(connectionString);
        return new AppDbContext(options.Options);
    }
}
