using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// Forces the MySQL session to UTC so stored timestamps match the UTC values written by the application.
/// </summary>
public sealed class UtcSessionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SET time_zone = '+00:00'";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SET time_zone = '+00:00'";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
