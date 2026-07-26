using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NT.QAMS.Application;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposed for WebApplicationFactory-based functional tests.
public partial class Program;
