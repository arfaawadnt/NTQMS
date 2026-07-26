using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Facility;
using NT.QAMS.Domain.Files;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.Organization;
using NT.QAMS.Domain.Records;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.Reporting;
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
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();
    public DbSet<Nonconformance> Nonconformances => Set<Nonconformance>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<QualityObjective> QualityObjectives => Set<QualityObjective>();
    public DbSet<QualityPolicy> QualityPolicies => Set<QualityPolicy>();
    public DbSet<FeedbackEntry> FeedbackEntries => Set<FeedbackEntry>();
    public DbSet<ControlledDocument> Documents => Set<ControlledDocument>();
    public DbSet<FileReference> Files => Set<FileReference>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<ReferenceStandard> ReferenceStandards => Set<ReferenceStandard>();
    public DbSet<CompetencyRecord> Competencies => Set<CompetencyRecord>();
    public DbSet<TestAuthorization> TestAuthorizations => Set<TestAuthorization>();
    public DbSet<MonitoringPoint> MonitoringPoints => Set<MonitoringPoint>();
    public DbSet<TrainingAssignment> TrainingAssignments => Set<TrainingAssignment>();
    public DbSet<RiskItem> Risks => Set<RiskItem>();
    public DbSet<ConflictDeclaration> ConflictDeclarations => Set<ConflictDeclaration>();
    public DbSet<InterestedParty> InterestedParties => Set<InterestedParty>();
    public DbSet<ContextIssue> ContextIssues => Set<ContextIssue>();
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
    public DbSet<MethodComparisonStudy> MethodComparisons => Set<MethodComparisonStudy>();
    public DbSet<LinearityStudy> LinearityStudies => Set<LinearityStudy>();
    public DbSet<DetectionLimitStudy> DetectionLimitStudies => Set<DetectionLimitStudy>();
    public DbSet<ReferenceIntervalStudy> ReferenceIntervalStudies => Set<ReferenceIntervalStudy>();
    public DbSet<SigmaAssessment> SigmaAssessments => Set<SigmaAssessment>();
    public DbSet<PrecisionStudy> PrecisionStudies => Set<PrecisionStudy>();
    public DbSet<OutlierScreening> OutlierScreenings => Set<OutlierScreening>();
    public DbSet<CarryoverStudy> CarryoverStudies => Set<CarryoverStudy>();
    public DbSet<LotComparisonStudy> LotComparisons => Set<LotComparisonStudy>();
    public DbSet<InterferenceStudy> InterferenceStudies => Set<InterferenceStudy>();
    public DbSet<InstrumentComparabilityStudy> InstrumentComparabilities => Set<InstrumentComparabilityStudy>();
    public DbSet<PtEnrollment> PtEnrollments => Set<PtEnrollment>();
    public DbSet<PtPlan> PtPlans => Set<PtPlan>();
    public DbSet<AuditTrailReview> AuditTrailReviews => Set<AuditTrailReview>();
    public DbSet<UncertaintyBudget> UncertaintyBudgets => Set<UncertaintyBudget>();
    public DbSet<ArchiveEntry> ArchiveEntries => Set<ArchiveEntry>();
    public DbSet<SlaDefinition> SlaDefinitions => Set<SlaDefinition>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<EscalationTimer> EscalationTimers => Set<EscalationTimer>();
    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();
    public DbSet<FieldChangeRecord> FieldChanges => Set<FieldChangeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Layer-1 isolation: tenant global query filter on every ITenantScoped
        // entity, applied by convention so it is structurally unforgettable.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Every identifier is minted by the domain (UUIDv7 in constructors or
            // at the creation site), never by EF or the database. Without this,
            // EF's Guid-key convention (ValueGeneratedOnAdd) makes change
            // detection treat a child added to an already-loaded aggregate as an
            // EXISTING row (key is set → assume persisted → Modified), issuing an
            // UPDATE that affects 0 rows and throws DbUpdateConcurrencyException.
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is not null)
            {
                foreach (var keyProperty in primaryKey.Properties.Where(p => p.ClrType == typeof(Guid)))
                {
                    keyProperty.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }
            }

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
