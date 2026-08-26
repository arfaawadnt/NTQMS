using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Accreditation;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.Committees;
using NT.QAMS.Domain.AuditManagement;
using NT.QAMS.Domain.Competency;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.Domain.Facility;
using NT.QAMS.Domain.Files;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.InfectionControl;
using NT.QAMS.Domain.Integration;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.Organization;
using NT.QAMS.Domain.PatientExperience;
using NT.QAMS.Domain.PatientSafety;
using NT.QAMS.Domain.QualityIndicators;
using NT.QAMS.Domain.Records;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.SupplierQuality;
using NT.QAMS.Domain.Tenancy;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.SharedKernel.MultiTenancy;

namespace NT.QAMS.Infrastructure.Persistence;

/// <summary>
/// The write-side DbContext. Schema layout per the database architecture:
/// saas (control plane, no RLS), qams (tenant data, RLS), audit (append-only).
/// Global tenant query filters are applied to every ITenantScoped entity by
/// convention - a module cannot forget them. IAllocatable entities additionally
/// get the per-user working-scope filter (allowed branches/departments) in the
/// same composed filter, so scope restriction is equally unforgettable.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant,
    IUserPrivileges? privileges = null) : DbContext(options), IAppDbContext
{
    private readonly ICurrentTenant _currentTenant = currentTenant;

    // Null-object for callers that construct the context directly (tests,
    // design-time factory): unrestricted, matching pre-scoping behaviour.
    private readonly IUserPrivileges _privileges =
        privileges ?? new Authorization.RequestPrivileges();

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Nonconformance> Nonconformances => Set<Nonconformance>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<QualityIndicator> QualityIndicators => Set<QualityIndicator>();
    public DbSet<StandardSet> StandardSets => Set<StandardSet>();
    public DbSet<EvidenceLink> EvidenceLinks => Set<EvidenceLink>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<QualityObjective> QualityObjectives => Set<QualityObjective>();
    public DbSet<QualityPolicy> QualityPolicies => Set<QualityPolicy>();
    public DbSet<UserAccessReview> UserAccessReviews => Set<UserAccessReview>();
    public DbSet<FeedbackEntry> FeedbackEntries => Set<FeedbackEntry>();
    public DbSet<ControlledDocument> Documents => Set<ControlledDocument>();
    public DbSet<DocumentAcknowledgement> DocumentAcknowledgements => Set<DocumentAcknowledgement>();
    public DbSet<DocumentControlledCopy> DocumentControlledCopies => Set<DocumentControlledCopy>();
    public DbSet<FileReference> Files => Set<FileReference>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<AuditProgram> AuditPrograms => Set<AuditProgram>();
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<SatisfactionSurvey> SatisfactionSurveys => Set<SatisfactionSurvey>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<IntegrationEndpoint> IntegrationEndpoints => Set<IntegrationEndpoint>();
    public DbSet<IntegrationMessage> IntegrationMessages => Set<IntegrationMessage>();
    public DbSet<PatientStay> PatientStays => Set<PatientStay>();
    public DbSet<PatientSafetyEvent> PatientSafetyEvents => Set<PatientSafetyEvent>();
    public DbSet<HaiCase> HaiCases => Set<HaiCase>();
    public DbSet<DeviceExposure> DeviceExposures => Set<DeviceExposure>();
    public DbSet<TrainingCourse> TrainingCourses => Set<TrainingCourse>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<ReferenceStandard> ReferenceStandards => Set<ReferenceStandard>();
    public DbSet<CompetencyRecord> Competencies => Set<CompetencyRecord>();
    public DbSet<TestAuthorization> TestAuthorizations => Set<TestAuthorization>();
    public DbSet<MonitoringPoint> MonitoringPoints => Set<MonitoringPoint>();
    public DbSet<TrainingAssignment> TrainingAssignments => Set<TrainingAssignment>();
    public DbSet<RiskItem> Risks => Set<RiskItem>();
    public DbSet<FmeaStudy> FmeaStudies => Set<FmeaStudy>();
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
    public DbSet<TenantMailSettings> MailSettings => Set<TenantMailSettings>();
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
    public DbSet<QualityHealthProfile> QualityHealthProfiles => Set<QualityHealthProfile>();
    public DbSet<FieldChangeRecord> FieldChanges => Set<FieldChangeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            // DB-009/VAL-003: PostgreSQL's xmin system column is the optimistic-
            // concurrency token on every aggregate root — zero schema change; a
            // lost update surfaces as DbUpdateConcurrencyException, which the
            // API maps to HTTP 409 (DomainExceptionHandler). Convention-applied
            // so a new module cannot forget it. (Npgsql-only: the InMemory
            // provider used by unit tests has no xmin.)
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(SharedKernel.Primitives.AggregateRoot).IsAssignableFrom(entityType.ClrType)
                    && entityType.BaseType is null)
                {
                    // Equivalent of Npgsql's UseXminAsConcurrencyToken(), which
                    // only ships a generic overload: map the xid system column
                    // as a store-generated shadow concurrency token.
                    modelBuilder.Entity(entityType.ClrType)
                        .Property<uint>("xmin")
                        .HasColumnType("xid")
                        .ValueGeneratedOnAddOrUpdate()
                        .IsConcurrencyToken();
                }
            }

            // MSG-006: the natural idempotency key for source-driven NCs
            // (audit finding, complaint, PT, excursion, …) — at-least-once
            // redelivery can never net a second NC for the same source, even
            // across concurrent processors. Partial index: manual NCs have no
            // source. (Npgsql-only: InMemory ignores index filters and would
            // reject the second manual NC.)
            modelBuilder.Entity<Nonconformance>()
                .HasIndex(n => new { n.TenantId, n.SourceRef })
                .IsUnique()
                .HasFilter("source_ref IS NOT NULL")
                .HasDatabaseName("ux_nonconformance_source");
        }

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
                // EF allows a single filter per entity, so tenant isolation and
                // (for allocatable records) the user working scope are one
                // composed expression, chosen here.
                var filter = typeof(IAllocatable).IsAssignableFrom(entityType.ClrType)
                    ? nameof(ApplyTenantAndScopeFilter)
                    : nameof(ApplyTenantFilter);
                var method = typeof(AppDbContext)
                    .GetMethod(filter,
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

    /// <summary>
    /// Tenant isolation plus the per-user working scope: a branch-restricted user
    /// sees records inside their branches and unattributed (null-branch) records,
    /// never records of other branches; likewise for departments. Unrestricted
    /// actors and background jobs see the whole tenant — <c>HasBranchRestriction</c>
    /// is false for them, which short-circuits the scope terms to true.
    /// </summary>
    private void ApplyTenantAndScopeFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped, IAllocatable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.TenantId == _currentTenant.TenantId
            && (!_privileges.HasBranchRestriction
                || e.BranchId == null
                || _privileges.AllowedBranchIds.Contains(e.BranchId.Value))
            && (!_privileges.HasDepartmentRestriction
                || e.DepartmentId == null
                || _privileges.AllowedDepartmentIds.Contains(e.DepartmentId.Value)));
    }
}
