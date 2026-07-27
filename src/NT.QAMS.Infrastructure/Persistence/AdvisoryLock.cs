using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace NT.QAMS.Infrastructure.Persistence;

/// <summary>
/// Leader election for recurring jobs (OPS-002 durable): runs the given unit of
/// work inside a transaction that holds a PostgreSQL transaction-scoped
/// advisory lock, so exactly one instance executes it at a time and the lock
/// can never leak (it dies with the transaction, even on crash). A contended
/// lock means another instance is already running this job — the caller skips
/// the round instead of duplicating it.
/// </summary>
public static class AdvisoryLock
{
    /// <summary>
    /// Attempts the lock and runs <paramref name="action"/> while holding it.
    /// Returns false without running when another session holds the key. On a
    /// non-relational provider (unit tests) the action simply runs — a single
    /// process needs no cross-instance election.
    /// </summary>
    public static async Task<bool> TryRunExclusiveAsync(
        AppDbContext db, long key, Func<Task> action, CancellationToken cancellationToken)
    {
        if (!db.Database.IsNpgsql())
        {
            await action();
            return true;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var command = db.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.CommandText = "SELECT pg_try_advisory_xact_lock(@key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "key";
            parameter.Value = key;
            command.Parameters.Add(parameter);
            command.Transaction = transaction.GetDbTransaction();

            if (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            {
                return false; // another instance leads this round — skip, don't duplicate
            }
        }

        await action();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
