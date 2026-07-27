using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NT.QAMS.Application;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Behaviors;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure;
using NT.QAMS.Infrastructure.Health;
using NT.QAMS.Infrastructure.Observability;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Security;
using NT.QAMS.WebApi.Middleware;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// OBS-001/002/003 — observability baseline.
// Logs: structured JSON console in Production (scopes carry service/tenant/
// trace/correlation); OTLP log export when Otlp:Endpoint is configured.
// Traces: HTTP → MediatR → EF(Npgsql) → Outbox/Jobs, one trace id end-to-end.
// Metrics: built-in ASP.NET RED + Npgsql pool + NT.QAMS instruments, scraped
// at /metrics (Prometheus) and optionally pushed over OTLP.
// ---------------------------------------------------------------------------
if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "O";
    });
}

var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        ObservabilityMiddleware.ServiceName,
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
        serviceInstanceId: Environment.MachineName));

openTelemetry.WithTracing(tracing =>
{
    tracing
        .AddAspNetCoreInstrumentation(options =>
            // Probe noise stays out of the trace store.
            options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                    && !ctx.Request.Path.StartsWithSegments("/metrics"))
        .AddNpgsql()
        .AddSource(ApplicationDiagnostics.ActivitySourceName)
        .AddSource(QamsDiagnostics.OutboxSourceName)
        .AddSource(QamsDiagnostics.JobsSourceName);
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    }
});

openTelemetry.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("Npgsql")
        .AddMeter(QamsMetrics.MeterName)
        .AddPrometheusExporter();
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    }
});

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeScopes = true;
        logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The HTTP-facing identity implementations override Infrastructure's defaults.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured (set the Jwt__Secret environment variable).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep JWT claim names verbatim ("sub", "name", "tenant_id") instead of
        // remapping them to legacy ClaimTypes.* URIs — so what we issue is exactly
        // what handlers read. (See HttpCurrentUser.)
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "nt-qams",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "nt-qams",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

// Deny-by-default: every endpoint requires an authenticated user unless it
// carries [AllowAnonymous] (login) or an explicit anonymous mapping (health).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
// OBS-002: every ProblemDetails (framework-produced included) carries the ids
// a client needs to quote in a support ticket.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier;
    if (ctx.HttpContext.Items[ObservabilityMiddleware.CorrelationItemKey] is string correlationId)
    {
        ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
    }
});

// OPS-008: readiness is DB-backed — the service only accepts traffic when
// PostgreSQL answers. Liveness stays check-free (a DB outage must recycle
// traffic, not the process).
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
builder.Services.AddHealthChecks()
    .AddCheck(
        PostgresReadinessHealthCheck.Name,
        new PostgresReadinessHealthCheck(postgresConnectionString),
        HealthStatus.Unhealthy,
        [PostgresReadinessHealthCheck.ReadyTag]);

var app = builder.Build();

// TENANT-004: deployment safety gate. Production refuses to boot when the DB
// connection role is over-privileged (SUPERUSER / BYPASSRLS / table owner) —
// any of those would void RLS tenant isolation and record immutability.
// Non-production logs the violations so dev setups stay honest about the gap.
try
{
    if (app.Environment.IsProduction())
    {
        await DatabaseRoleGuard.EnsureLeastPrivilegeAsync(postgresConnectionString);
    }
    else
    {
        foreach (var violation in await DatabaseRoleGuard.FindViolationsAsync(postgresConnectionString))
        {
            app.Logger.LogWarning(
                "Database role guard ({Environment}): {Violation} — Production will refuse to start; " +
                "see deploy/harden-runtime-role.sql",
                app.Environment.EnvironmentName, violation);
        }
    }
}
catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
{
    // Unreachable database is a readiness concern, not a privilege violation:
    // /health/ready stays Unhealthy until PostgreSQL answers.
    app.Logger.LogWarning(ex,
        "Database role guard: could not verify the connection role's privileges (database unreachable).");
}

// Deployment convenience: apply pending migrations at startup when explicitly
// enabled (small installs / first boot). Pipeline deployments should prefer the
// idempotent SQL script in deploy/ and keep this off.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Bootstrap the platform administrator from configuration (idempotent).
// Required once per environment; without it no one can provision tenants.
var bootstrapEmail = app.Configuration["PlatformAdmin:Email"];
var bootstrapPassword = app.Configuration["PlatformAdmin:Password"];
if (!string.IsNullOrWhiteSpace(bootstrapEmail) && !string.IsNullOrWhiteSpace(bootstrapPassword))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    var email = bootstrapEmail.Trim().ToLowerInvariant();
    var exists = await db.Users.AnyAsync(u => u.TenantId == null && u.Email == email);
    if (!exists)
    {
        db.Users.Add(UserAccount.Create(
            null, email, "Platform Administrator", hasher.Hash(bootstrapPassword), UserRole.PlatformAdmin));
        await db.SaveChangesAsync();
    }
}

// Backfill the starter list-of-values for tenants provisioned before the
// default catalog existed (or before a category was added to it). Additive and
// per-category idempotent — curated lists are never touched.
{
    using var scope = app.Services.CreateScope();
    // Cross-tenant backfill runs as trusted infrastructure — elevate past RLS.
    scope.ServiceProvider.GetRequiredService<NT.QAMS.Application.Abstractions.ICurrentTenantSetter>().Elevate();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync();
    var seeded = 0;
    foreach (var tenantId in tenantIds)
    {
        seeded += await NT.QAMS.Application.Organization.DefaultLovCatalog.SeedMissingAsync(db, tenantId, CancellationToken.None);
    }

    if (seeded > 0)
    {
        await db.SaveChangesAsync();
        app.Logger.LogInformation("LOV backfill: {Count} starter entries added across {Tenants} tenant(s)", seeded, tenantIds.Count);
    }
}

// First in the pipeline: correlation + the canonical completion log cover
// every response, including ones produced by the exception handler below.
app.UseMiddleware<ObservabilityMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<ActiveSessionMiddleware>();
app.UseMiddleware<MfaEnrollmentGateMiddleware>();
app.UseMiddleware<ChangeReasonMiddleware>();
app.UseAuthorization();

app.MapControllers();

// OPS-008: /health/live = process is up (no checks — never fails on DB loss);
// /health/ready = live AND PostgreSQL answering (503 otherwise) — the probe
// target for the container HEALTHCHECK and any LB/orchestrator readiness gate.
// /health remains as the legacy liveness alias (existing probes/scripts).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(PostgresReadinessHealthCheck.ReadyTag),
}).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

// OBS-003: Prometheus scrape endpoint (RED + Npgsql pool + NT.QAMS meters).
// Anonymous like the health probes — measurements only, no tenant data.
app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();

app.Run();

// Exposed for WebApplicationFactory-based functional tests.
public partial class Program;
