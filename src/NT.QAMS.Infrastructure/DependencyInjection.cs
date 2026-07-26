using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NT.QAMS.Application.Abstractions;
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

        services.AddScoped<AuditStampInterceptor>();
        services.AddScoped<FieldChangeInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<OutboxInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(configuration.GetConnectionString("Postgres"))
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

        services.AddSingleton<IPasswordHasher, Security.IdentityPasswordHasher>();
        services.AddSingleton(new PasswordPolicyOptions(
            int.TryParse(configuration["PasswordPolicy:MaxAgeDays"], out var maxAge) ? maxAge : 90,
            int.TryParse(configuration["PasswordPolicy:HistoryDepth"], out var depth) ? depth : 5));
        services.AddSingleton(new SecurityOptions(
            bool.TryParse(configuration["Security:RequireMfaForPrivilegedRoles"], out var requireMfa) && requireMfa));
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

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<Jobs.ScheduledSweepService>();
        services.AddHostedService<Jobs.KpiSnapshotService>();

        return services;
    }
}
