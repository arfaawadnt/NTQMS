using Npgsql;

namespace NT.QAMS.Infrastructure.Security;

/// <summary>
/// Deployment safety gate (TENANT-004): verifies that the database connection
/// role is the least-privilege runtime role (<c>qams_app</c>) and not an
/// over-privileged one. A SUPERUSER or BYPASSRLS role silently disables
/// Row-Level Security, and a role that owns the application tables can drop
/// the RLS policies and immutability triggers — either would void the tenant
/// isolation (F-01) and signed-record protection (F-02) guarantees.
/// Production start-up refuses to boot on any violation; see
/// <c>deploy/harden-runtime-role.sql</c> for the remediation.
/// </summary>
public static class DatabaseRoleGuard
{
    /// <summary>Schemas whose table ownership disqualifies the runtime role.</summary>
    private static readonly string[] ApplicationSchemas = ["qams", "audit", "read", "saas", "ref"];

    /// <summary>Ceiling on how long the privilege probe may block start-up, in seconds.</summary>
    private const int ProbeTimeoutSeconds = 5;

    /// <summary>
    /// Inspects the role the connection string authenticates as and returns the
    /// list of least-privilege violations (empty when the role is safe).
    /// Throws <see cref="NpgsqlException"/> when the database is unreachable —
    /// the caller decides whether that is fatal (readiness handles DB-down).
    /// </summary>
    public static async Task<IReadOnlyList<string>> FindViolationsAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = ProbeTimeoutSeconds,
            CommandTimeout = ProbeTimeoutSeconds,
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT r.rolname::text,
                   r.rolsuper,
                   r.rolbypassrls,
                   (SELECT count(*) FROM pg_tables t
                     WHERE t.schemaname = ANY(@schemas)
                       AND pg_has_role(current_user, t.tableowner::regrole, 'USAGE'))
            FROM pg_roles r
            WHERE r.rolname = current_user
            """;
        command.Parameters.AddWithValue("schemas", ApplicationSchemas);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var role = reader.GetString(0);
        var isSuperuser = reader.GetBoolean(1);
        var hasBypassRls = reader.GetBoolean(2);
        var ownedTables = reader.GetInt64(3);

        var violations = new List<string>();
        if (isSuperuser)
        {
            violations.Add($"connection role '{role}' is a SUPERUSER — Row-Level Security is not enforced for it");
        }

        if (hasBypassRls)
        {
            violations.Add($"connection role '{role}' has BYPASSRLS — Row-Level Security is not enforced for it");
        }

        if (ownedTables > 0)
        {
            violations.Add(
                $"connection role '{role}' owns (or inherits ownership of) {ownedTables} application table(s) — " +
                "an owner can drop the RLS policies and immutability triggers");
        }

        return violations;
    }

    /// <summary>
    /// Production start-up gate: throws (refusing to boot) when the connection
    /// role is over-privileged. The exception message carries the remediation.
    /// </summary>
    public static async Task EnsureLeastPrivilegeAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        var violations = await FindViolationsAsync(connectionString, cancellationToken);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Refusing to start with an over-privileged database role: " +
                string.Join("; ", violations) +
                ". Run the application as the least-privilege runtime role (qams_app) — " +
                "provision it with deploy/harden-runtime-role.sql (migrations run separately as qams_owner).");
        }
    }
}
