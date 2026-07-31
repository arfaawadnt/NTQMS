using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Authorization;
using NT.QAMS.Application.Organization;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Persistence;

namespace NT.QAMS.WebApi.Startup;

/// <summary>
/// OPS-010: the two idempotent startup data steps — bootstrapping the platform
/// administrator and back-filling the starter list-of-values catalogue.
/// <para>
/// They used to run inline before the host started listening, so a cold start
/// against an unreachable database killed the process instead of letting
/// <c>/health/ready</c> report the outage: a database blip during a restart
/// became a crash-loop with nothing to interrogate. They now run through
/// <see cref="TryRunAsync"/>, which completes normally on a healthy boot
/// (keeping the deterministic pre-listen ordering the tests rely on) and
/// <i>defers</i> when the database is simply not answering yet —
/// <see cref="DeferredStartupSeeder"/> then retries until it is.
/// </para>
/// Both steps are idempotent, so running them again later is safe.
/// </summary>
public static class StartupSeeding
{
    /// <summary>
    /// Runs the seeding steps, returning <see langword="true"/> when they
    /// completed and <see langword="false"/> when the database was unavailable
    /// and the work must be retried later. Faults that are NOT connectivity
    /// problems still propagate — a genuinely broken seed must fail loudly.
    /// </summary>
    public static async Task<bool> TryRunAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(services, configuration, cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            logger.LogWarning(ex,
                "Startup data seeding deferred: the database is not answering yet. The application is starting " +
                "anyway so /health/ready reports the outage (OPS-010); seeding retries automatically.");
            return false;
        }
    }

    /// <summary>Performs the idempotent seeding steps. Throws on failure.</summary>
    public static async Task RunAsync(
        IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        await BootstrapPlatformAdminAsync(services, configuration, cancellationToken);
        await BackfillStarterListOfValuesAsync(services, cancellationToken);
        await BackfillRolesAndAssignmentsAsync(services, cancellationToken);
    }

    /// <summary>
    /// Bootstrap the platform administrator from configuration (idempotent).
    /// Required once per environment; without it no one can provision tenants.
    /// </summary>
    private static async Task BootstrapPlatformAdminAsync(
        IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var bootstrapEmail = configuration["PlatformAdmin:Email"];
        var bootstrapPassword = configuration["PlatformAdmin:Password"];
        if (string.IsNullOrWhiteSpace(bootstrapEmail) || string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = bootstrapEmail.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.TenantId == null && u.Email == email, cancellationToken);
        if (!exists)
        {
            db.Users.Add(UserAccount.Create(
                null, email, "Platform Administrator", hasher.Hash(bootstrapPassword), UserRole.PlatformAdmin));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Backfill the starter list-of-values for tenants provisioned before the
    /// default catalog existed (or before a category was added to it). Additive
    /// and per-category idempotent — curated lists are never touched.
    /// </summary>
    private static async Task<int> BackfillStarterListOfValuesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        // Cross-tenant backfill runs as trusted infrastructure — elevate past RLS.
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync(cancellationToken);
        var seeded = 0;
        foreach (var tenantId in tenantIds)
        {
            seeded += await DefaultLovCatalog.SeedMissingAsync(db, tenantId, cancellationToken);
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return seeded;
    }

    /// <summary>
    /// Gives every tenant the starter roles, and puts any account that has no role
    /// yet onto the seeded role matching the fixed tier it already held.
    /// <para>
    /// This is the upgrade path to configurable privileges, and it is idempotent:
    /// accounts that already carry a role are never re-pointed, so an administrator
    /// who moved someone to a custom role does not find them moved back on the next
    /// restart.
    /// </para>
    /// </summary>
    private static async Task<int> BackfillRolesAndAssignmentsAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<ICurrentTenantSetter>().Elevate();

        var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync(cancellationToken);
        var changes = 0;

        foreach (var tenantId in tenantIds)
        {
            changes += (await SystemRoleCatalog.SeedMissingAsync(db, tenantId, cancellationToken)).Count;
        }

        if (changes > 0)
        {
            // Save first: the assignment step below needs the new roles to have ids
            // it can resolve by name.
            await db.SaveChangesAsync(cancellationToken);
        }

        var roleIdsByTenant = await db.Roles.IgnoreQueryFilters()
            .Select(r => new { r.TenantId, r.Id, r.Name })
            .ToListAsync(cancellationToken);

        var lookup = roleIdsByTenant
            .GroupBy(r => r.TenantId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Name, r => r.Id, StringComparer.Ordinal));

        var unassigned = await db.Users
            .Where(u => u.RoleId == null && u.TenantId != null)
            .ToListAsync(cancellationToken);

        var assigned = 0;
        foreach (var user in unassigned)
        {
            if (user.TenantId is not { } tenantId
                || !lookup.TryGetValue(tenantId, out var roles)
                || !roles.TryGetValue(SystemRoleCatalog.RoleNameFor(user.Role), out var roleId))
            {
                continue;
            }

            user.AssignRole(roleId);
            assigned++;
        }

        if (assigned > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return changes + assigned;
    }

    /// <summary>
    /// True when the failure means "the database is not reachable/ready yet"
    /// rather than "this seed is broken": socket/connection failures, timeouts,
    /// and the two PostgreSQL states that mean the schema or database has not
    /// been created yet (migrations may still be running in a separate step).
    /// </summary>
    internal static bool IsDatabaseUnavailable(Exception? exception) => exception switch
    {
        null => false,
        // 42P01 undefined_table, 3D000 invalid_catalog_name — schema not ready yet.
        PostgresException pg => pg.SqlState is "42P01" or "3D000",
        // A connectivity-level Npgsql failure (PostgresException is handled above).
        NpgsqlException => true,
        SocketException or TimeoutException => true,
        _ => IsDatabaseUnavailable(exception.InnerException),
    };
}
