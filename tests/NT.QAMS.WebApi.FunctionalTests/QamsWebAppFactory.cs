using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Boots the real WebApi host (real JWT issuance + JwtBearer validation +
/// HttpCurrentUser claim reading — the pipeline that the unit tests bypass with a
/// fake current-user) but swaps PostgreSQL for EF InMemory and the raw-SQL
/// reference generator for a sequential one, so the whole flow runs without a
/// database. The interceptors (tenant/audit/outbox) are kept so tenant stamping
/// still works.
/// </summary>
public sealed class QamsWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"functional-{Guid.NewGuid():N}";

    public const string PlatformAdminEmail = "platform@test.local";
    public const string PlatformAdminPassword = "Platform-Test-Pass-1!";

    public ConcurrentQueue<string> ServerErrors { get; } = new();

    public QamsWebAppFactory()
    {
        // Set via environment variables (not ConfigureAppConfiguration): with
        // minimal hosting these reliably override the empty appsettings.json
        // defaults, exactly as a real deployment supplies them.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=placeholder;Username=x;Password=x");
        Environment.SetEnvironmentVariable("Jwt__Secret", "functional-test-secret-key-at-least-32-chars!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "nt-qams");
        Environment.SetEnvironmentVariable("Jwt__Audience", "nt-qams");
        Environment.SetEnvironmentVariable("PlatformAdmin__Email", PlatformAdminEmail);
        Environment.SetEnvironmentVariable("PlatformAdmin__Password", PlatformAdminPassword);
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "false");
        // SEC-013: the suite logs in liberally — keep the shared-process limits
        // out of the way; the rate-limit tests tighten them per-host instead.
        Environment.SetEnvironmentVariable("RateLimit__GlobalPermitPerMinute", "100000");
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitPerMinute", "100000");
        Environment.SetEnvironmentVariable("RateLimit__ESignaturePermitPerMinute", "100000");
        Environment.SetEnvironmentVariable("RateLimit__RefreshPermitPerMinute", "100000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new CapturingLoggerProvider(ServerErrors));
            logging.SetMinimumLevel(LogLevel.Error);
        });

        builder.ConfigureTestServices(services =>
        {
            // Strip every EF registration tied to AppDbContext (EF Core 9 also
            // registers IDbContextOptionsConfiguration<AppDbContext>), otherwise
            // both the Npgsql and InMemory providers stay registered and EF throws.
            RemoveWhere(services, d =>
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) ?? false) ||
                (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) ?? false));

            services.AddDbContext<AppDbContext>((sp, o) => o
                .UseInMemoryDatabase(_dbName)
                .AddInterceptors(
                    sp.GetRequiredService<AuditStampInterceptor>(),
                    sp.GetRequiredService<TenantStampInterceptor>(),
                    sp.GetRequiredService<OutboxInterceptor>()));

            RemoveWhere(services, d => d.ServiceType == typeof(IReferenceNumberGenerator));
            services.AddScoped<IReferenceNumberGenerator, SequentialRefGenerator>();

            // The background sweeps/outbox processor are not needed for these tests.
            RemoveWhere(services, d => d.ServiceType == typeof(IHostedService));
        });
    }

    private static void RemoveWhere(IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        foreach (var descriptor in services.Where(predicate).ToList())
        {
            services.Remove(descriptor);
        }
    }
}

/// <summary>Captures server-side error logs (incl. exceptions) so tests can surface root causes.</summary>
public sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);
    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                sink.Enqueue($"[{category}] {formatter(state, exception)} :: {exception?.GetType().Name}: {exception?.Message}");
            }
        }
    }
}

/// <summary>InMemory-safe stand-in for the Postgres raw-SQL counter.</summary>
public sealed class SequentialRefGenerator : IReferenceNumberGenerator
{
    private static int _counter;

    public Task<string> NextAsync(Guid tenantId, string refType, CancellationToken cancellationToken)
    {
        var next = Interlocked.Increment(ref _counter);
        return Task.FromResult($"{refType}-2026-{next:0000}");
    }
}
