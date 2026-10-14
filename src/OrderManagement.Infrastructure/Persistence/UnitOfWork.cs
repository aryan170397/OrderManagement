using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using OrderManagement.Application.Exceptions;
using OrderManagement.Application.Interfaces;

namespace OrderManagement.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private const int MaxAttempts = 3;

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (attempt < MaxAttempts && IsTransient(exception))
            {
                logger.LogWarning(
                    exception,
                    "Transient database error on attempt {Attempt}. The transaction will be retried.",
                    attempt);
                await SafeRollbackAsync(transaction, cancellationToken);
            }
            catch (Exception exception) when (exception is not AppException)
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                if (IsUniqueViolation(exception))
                {
                    throw new ConflictException("A record with the same unique value already exists.", "duplicate_resource");
                }

                throw;
            }
            catch
            {
                await SafeRollbackAsync(transaction, cancellationToken);
                throw;
            }
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException("A record with the same unique value already exists.", "duplicate_resource");
        }
    }

    private static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mySql && mySql.Number is 1205 or 1213 or 1040 or 1159 or 2006 or 2013)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException { Number: 1062 })
            {
                return true;
            }
        }

        return false;
    }

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception)
        {
            // The server may already have rolled the transaction back after a connection failure.
        }
    }
}
