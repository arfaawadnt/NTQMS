using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NT.QAMS.Domain.ComplianceLedger;
using Xunit;

namespace NT.QAMS.IntegrationTests;

/// <summary>
/// Schema hardening Phase 2: the two RLS gaps the discovery step measured are
/// closed. <c>audit.security_event</c> had the append-only trigger but no
/// policy — its reads leaked every tenant's events; <c>qams.ref_counter</c>
/// carried a NOT NULL tenant with no policy at all. Both now behave like every
/// other tenant table: isolated reads, fail-closed nil tenant, WITH CHECK on
/// writes — with security_event additionally allowing the pre-authentication
/// null-tenant write (failed logins have no tenant yet).
/// </summary>
[Collection("real-postgres")]
public sealed class SecurityEventRlsTests(RealPostgresFixture fx)
{
    private static SecurityEvent Event(Guid? tenantId, string type) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId,
        EventType = type,
        OccurredAtUtc = DateTimeOffset.UtcNow,
    };

    [SkippableFact]
    public async Task Security_events_are_tenant_isolated_and_preauth_rows_are_platform_only()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var marker = $"RLS_ITEST_{Guid.CreateVersion7().ToString("N")[..8]}";
        db.Set<SecurityEvent>().AddRange(
            Event(tenantA, marker), Event(tenantB, marker), Event(null, marker));
        await db.SaveChangesAsync();

        async Task<int> VisibleAs(Guid? tenant, bool bypass)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', {1}, true)",
                tenant?.ToString() ?? string.Empty, bypass ? "on" : "off");
            return await db.Set<SecurityEvent>().CountAsync(e => e.EventType == marker);
        }

        (await VisibleAs(tenantA, false)).Should().Be(1, "a tenant sees only its own events, not pre-auth ones");
        (await VisibleAs(tenantB, false)).Should().Be(1);
        (await VisibleAs(null, false)).Should().Be(0, "an unresolved tenant is fail-closed");
        (await VisibleAs(tenantA, true)).Should().Be(3, "elevation sees tenant and platform events alike");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Security_event_write_check_allows_own_and_preauth_but_rejects_foreign()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out _);
        await using var tx = await db.Database.BeginTransactionAsync();

        var mine = Guid.CreateVersion7();
        var theirs = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', 'off', true)",
            mine.ToString());

        db.Set<SecurityEvent>().Add(Event(mine, "RLS_ITEST_OWN"));
        await db.SaveChangesAsync();

        db.Set<SecurityEvent>().Add(Event(null, "RLS_ITEST_PREAUTH"));
        await db.SaveChangesAsync();

        db.Set<SecurityEvent>().Add(Event(theirs, "RLS_ITEST_FOREIGN"));
        var act = () => db.SaveChangesAsync();
        (await act.Should().ThrowAsync<DbUpdateException>("WITH CHECK must reject a foreign tenant id"))
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be("42501");

        await tx.RollbackAsync();
    }

    [SkippableFact]
    public async Task Ref_counter_is_tenant_isolated()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);
        ctx.Elevate();
        await using var tx = await db.Database.BeginTransactionAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO qams.ref_counter (tenant_id, ref_type, year, last_value) VALUES ({0}, 'ITEST', 2099, 1), ({1}, 'ITEST', 2099, 7)",
            tenantA, tenantB);

        async Task<long?> LastValueAs(Guid? tenant)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant', {0}, true), set_config('app.bypass_rls', 'off', true)",
                tenant?.ToString() ?? string.Empty);
            return await db.Database.SqlQueryRaw<long?>(
                    "SELECT max(last_value) AS \"Value\" FROM qams.ref_counter WHERE ref_type = 'ITEST' AND year = 2099")
                .SingleAsync();
        }

        (await LastValueAs(tenantA)).Should().Be(1, "tenant A sees only its own counter");
        (await LastValueAs(tenantB)).Should().Be(7);
        (await LastValueAs(null)).Should().BeNull("an unresolved tenant sees no counters");

        await tx.RollbackAsync();
    }

    /// <summary>
    /// Pins the login-flow regression this phase's live check caught: a
    /// tenant-stamped security event written on a request whose connection GUC
    /// carries that same tenant (the LoginHandler now scopes the request as soon
    /// as the slug resolves) must be accepted; without the scope it was 42501.
    /// </summary>
    [SkippableFact]
    public async Task Login_shaped_write_passes_when_the_request_is_scoped_to_the_events_tenant()
    {
        Skip.IfNot(fx.Available, fx.Unavailable ?? "PostgreSQL unavailable");

        using var db = fx.CreateContext(out var ctx);

        // Order matters, as it does in production: LoginHandler scopes the tenant
        // BEFORE any database work, so the interceptor stamps the GUC when the
        // connection opens. Scoping after the connection is open (e.g. inside an
        // already-begun transaction) would be too late - which is the bug shape
        // this pin exists to catch.
        var tenant = Guid.CreateVersion7();
        ctx.Set(tenant); // what LoginHandler now does once the slug resolves
        await using var tx = await db.Database.BeginTransactionAsync();

        db.Set<SecurityEvent>().Add(Event(tenant, "RLS_ITEST_LOGIN"));
        var act = () => db.SaveChangesAsync();
        await act.Should().NotThrowAsync("the scoped request carries the tenant the event is stamped with");

        await tx.RollbackAsync();
    }
}
