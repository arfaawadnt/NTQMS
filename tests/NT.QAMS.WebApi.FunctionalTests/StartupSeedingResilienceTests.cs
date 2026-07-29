using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.WebApi.Startup;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Regression for finding OPS-010 (raised by the witnessed OQ execution on
/// 2026-07-29): the startup data seeding used to run inline and throw, so a cold
/// start against an unreachable database killed the process instead of starting
/// and letting <c>/health/ready</c> report the outage — a database blip during a
/// restart became a crash-loop.
/// <para>
/// The earlier readiness evidence never caught this because every test took the
/// database down <i>after</i> startup (and the functional host swaps EF for the
/// InMemory provider, so its "database down" only breaks the health check).
/// </para>
/// </summary>
public sealed class StartupSeedingResilienceTests
{
    /// <summary>Nothing listens here — the cold-start-with-database-down case.</summary>
    private const string UnreachableConnectionString =
        "Host=localhost;Port=5433;Database=ntqams;Username=x;Password=x;Timeout=2;Command Timeout=2";

    [Fact]
    public async Task An_unreachable_database_defers_the_seeding_instead_of_throwing()
    {
        await using var services = BuildServices(UnreachableConnectionString);

        var completed = await StartupSeeding.TryRunAsync(
            services, Config(), NullLogger.Instance, CancellationToken.None);

        completed.Should().BeFalse(
            "an unreachable database must DEFER the seeding (DeferredStartupSeeder retries) — " +
            "throwing here is what crashed the host before OPS-010 was fixed");
    }

    [Fact]
    public async Task Seeding_completes_and_is_idempotent_when_the_database_answers()
    {
        await using var services = BuildInMemoryServices();

        var first = await StartupSeeding.TryRunAsync(
            services, Config(), NullLogger.Instance, CancellationToken.None);
        var second = await StartupSeeding.TryRunAsync(
            services, Config(), NullLogger.Instance, CancellationToken.None);

        first.Should().BeTrue("a healthy boot still seeds inline, before the first request is served");
        second.Should().BeTrue("both steps are idempotent, so the deferred retry is always safe");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admins = await db.Users.CountAsync(u => u.TenantId == null && u.Email == "seed-probe@test.local");
        admins.Should().Be(1, "running the bootstrap twice must not duplicate the platform administrator");
    }

    [Theory]
    [MemberData(nameof(UnavailableFailures))]
    public void Connectivity_failures_are_classified_as_database_unavailable(Exception failure) =>
        StartupSeeding.IsDatabaseUnavailable(failure).Should().BeTrue();

    [Fact]
    public void A_real_seed_fault_is_not_swallowed() =>
        StartupSeeding.IsDatabaseUnavailable(new InvalidOperationException("a genuine bug"))
            .Should().BeFalse("only connectivity failures may be deferred — real faults must still fail loudly");

    public static TheoryData<Exception> UnavailableFailures() =>
    [
        new NpgsqlException("Failed to connect to 127.0.0.1:5433"),
        new TimeoutException("connection timed out"),
        // The wrapped shape EF Core produces.
        new InvalidOperationException("outer", new NpgsqlException("inner connectivity failure")),
    ];

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlatformAdmin:Email"] = "seed-probe@test.local",
                ["PlatformAdmin:Password"] = "Seed-Probe-Pass-1!",
            })
            .Build();

    private static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        AddSeedingDependencies(services);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildInMemoryServices()
    {
        // One database name per provider — evaluated ONCE, not per DbContext, so
        // every scope in the test shares the store the seeding wrote to.
        var databaseName = $"seed-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        AddSeedingDependencies(services);
        return services.BuildServiceProvider();
    }

    private static void AddSeedingDependencies(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IPasswordHasher, StubHasher>();
        services.AddSingleton<StubTenantContext>();
        services.AddSingleton<ICurrentTenantSetter>(sp => sp.GetRequiredService<StubTenantContext>());
        services.AddSingleton<ICurrentTenant>(sp => sp.GetRequiredService<StubTenantContext>());
    }

    private sealed class StubHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed::{password}";

        public bool Verify(string hash, string password) => hash == $"hashed::{password}";
    }

    /// <summary>Elevated (RLS-bypassing) tenant context, as the backfill requires.</summary>
    private sealed class StubTenantContext : ICurrentTenantSetter, ICurrentTenant
    {
        public Guid? TenantId { get; private set; }

        public bool IsResolved => TenantId is not null;

        public bool IsElevated { get; private set; }

        public void Set(Guid tenantId) => TenantId = tenantId;

        public void Clear() => TenantId = null;

        public void Elevate() => IsElevated = true;
    }
}
