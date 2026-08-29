using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Application.Abstractions;

namespace NT.QAMS.Infrastructure.Services;

/// <summary>PostgreSQL implementation of <see cref="IDatabaseErrorClassifier"/> (M-12).</summary>
public sealed class NpgsqlDatabaseErrorClassifier : IDatabaseErrorClassifier
{
    public bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
