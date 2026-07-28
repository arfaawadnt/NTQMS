using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Abstractions;

namespace NT.QAMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentTenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

        services.AddScoped<CurrentChangeReason>();
        services.AddScoped<ICurrentChangeReason>(sp => sp.GetRequiredService<CurrentChangeReason>());
        services.AddScoped<ICurrentChangeReasonSetter>(sp => sp.GetRequiredService<CurrentChangeReason>());

        services.AddScoped<AuditStampInterceptor>();
        services.AddScoped<FieldChangeInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<OutboxInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options
                // OPS-009: transient faults retry with backoff; every command
                // carries a hard timeout so a hung statement can't wedge a
                // request thread. User-initiated transactions wrap in the
                // execution strategy (see AdvisoryLock).
                .UseNpgsql(configuration.GetConnectionString("Postgres"), npgsql => npgsql
                    .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null)
                    .CommandTimeout(30))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    // Layer-2 isolation runs first: the tenant GUCs must be set on
                    // the connection before any query the other interceptors trigger.
                    sp.GetRequiredService<TenantConnectionInterceptor>(),
                    sp.GetRequiredService<AuditStampInterceptor>(),
                    sp.GetRequiredService<TenantStampInterceptor>(),
                    sp.GetRequiredService<FieldChangeInterceptor>(),
                    sp.GetRequiredService<OutboxInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // CQRS-004: replayed-command store; the WebApi overrides the accessor
        // with the Idempotency-Key header reader (background scopes have none).
        services.AddScoped<IIdempotencyStore, Persistence.Idempotency.EfIdempotencyStore>();
        services.AddSingleton<IIdempotencyKeyAccessor, Persistence.Idempotency.NullIdempotencyKeyAccessor>();

        services.AddSingleton<IPasswordHasher, Security.IdentityPasswordHasher>();
        // CFG-001/002: present-but-invalid values REFUSE startup (ConfigGuard)
        // instead of silently applying the default.
        services.AddSingleton(new PasswordPolicyOptions(
            Configuration.ConfigGuard.ReadInt(configuration, "PasswordPolicy:MaxAgeDays", 90),
            Configuration.ConfigGuard.ReadInt(configuration, "PasswordPolicy:HistoryDepth", 5)));
        services.AddSingleton(new SecurityOptions(
            Configuration.ConfigGuard.ReadBool(configuration, "Security:RequireMfaForPrivilegedRoles", false)));

        // F-16: QC acceptance limits are a controlled parameter set — configurable
        // (AnalyticalQuality:Westgard:*) rather than hard-coded, defaulting to the
        // standard Westgard multi-rule thresholds. Validated at startup so a bad
        // configuration fails fast instead of silently mis-grading QC.
        services.AddSingleton(new WestgardLimits(
            Configuration.ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:WarningSd", 2m),
            Configuration.ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:RejectSd", 3m),
            Configuration.ConfigGuard.ReadDecimal(configuration, "AnalyticalQuality:Westgard:RangeSd", 4m),
            Configuration.ConfigGuard.ReadInt(configuration, "AnalyticalQuality:Westgard:RunLength", 10)).Validated());
        // ADR-0009: rotating refresh sessions; lifetime is the sign-in horizon.
        services.AddSingleton(new Application.IdentityAccess.Commands.RefreshSessionOptions(
            Configuration.ConfigGuard.ReadInt(configuration, "Auth:RefreshTokenDays", 14)).Validated());
        services.AddSingleton<IJwtTokenService, Security.JwtTokenService>();
        services.AddSingleton<ITotpService, Security.TotpService>();
        services.AddScoped<IReferenceNumberGenerator, PostgresReferenceNumberGenerator>();
        services.AddSingleton<IExportService, Exports.ExportService>();
        services.AddScoped<ISecurityEventLog, Compliance.SecurityEventLog>();
        services.AddScoped<IESignatureService, Compliance.ESignatureService>();
        services.AddScoped<IComplianceLedgerStore, Compliance.ComplianceLedgerStore>();
        services.AddSingleton<IFileStorage, Storage.LocalFileStorage>();

        services.AddScoped<Application.Notifications.NotificationDispatcher>();
        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
        {
            services.AddSingleton<Application.Notifications.IEmailSender, Email.LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton<Application.Notifications.IEmailSender, Email.SmtpEmailSender>();
        }

        // MSG-007: processed outbox rows are transport, not the record (the
        // hash-chained ledger keeps the history) — purge after the window.
        services.AddSingleton(new OutboxOptions(
            Configuration.ConfigGuard.ReadInt(configuration, "Outbox:RetentionDays", 30)).Validated());

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<Jobs.ScheduledSweepService>();
        services.AddHostedService<Jobs.KpiSnapshotService>();
        // OPS-002: single-replica topology sentinel — warns when a second
        // instance runs against the same database (see ADR-0001).
        services.AddHostedService<Jobs.SingleReplicaGuardService>();

        return services;
    }
}
