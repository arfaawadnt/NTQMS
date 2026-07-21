using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Files;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.Organization;
using NT.QAMS.Domain.Records;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.SharedKernel.MultiTenancy;

namespace NT.QAMS.Infrastructure.Persistence;

/// <summary>
/// The write-side DbContext. Schema layout per the database architecture:
/// saas (control plane, no RLS), qams (tenant data, RLS), audit (append-only).
/// Global tenant query filters are applied to every ITenantScoped entity by
/// convention - a module cannot forget them.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant) : DbContext(options), IAppDbContext
{
    private readonly ICurrentTenant _currentTenant = currentTenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Nonconformance> Nonconformances => Set<Nonconformance>();
    public DbSet<ControlledDocument> Documents => Set<ControlledDocument>();
    public DbSet<FileReference> Files => Set<FileReference>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<CompetencyRecord> Competencies => Set<CompetencyRecord>();
    public DbSet<TrainingAssignment> TrainingAssignments => Set<TrainingAssignment>();
    public DbSet<RiskItem> Risks => Set<RiskItem>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ManagementReview> ManagementReviews => Set<ManagementReview>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierEvaluation> SupplierEvaluations => Set<SupplierEvaluation>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<TestCatalogItem> TestCatalogItems => Set<TestCatalogItem>();
    public DbSet<LovEntry> LovEntries => Set<LovEntry>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationDispatch> NotificationDispatches => Set<NotificationDispatch>();
    public DbSet<QcProfile> QcProfiles => Set<QcProfile>();
    public DbSet<QcRun> QcRuns => Set<QcRun>();
    public DbSet<ValidationStudy> ValidationStudies => Set<ValidationStudy>();
    public DbSet<PtEnrollment> PtEnrollments => Set<PtEnrollment>();
    public DbSet<ArchiveEntry> ArchiveEntries => Set<ArchiveEntry>();
    public DbSet<SlaDefinition> SlaDefinitions => Set<SlaDefinition>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<EscalationTimer> EscalationTimers => Set<EscalationTimer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Layer-1 isolation: tenant global query filter on every ITenantScoped
        // entity, applied by convention so it is structurally unforgettable.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
    }
}
