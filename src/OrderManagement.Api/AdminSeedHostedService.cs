using OrderManagement.Infrastructure.Persistence.Seed;

namespace OrderManagement.Api;

public sealed class AdminSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("AdminSeed:ApplyOnStartup", true))
        {
            logger.LogInformation("Administrator seed on startup is disabled.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            await AdminSeeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Administrator seed failed. Apply migrations and verify AdminSeed settings.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
