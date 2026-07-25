using Microsoft.EntityFrameworkCore;
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

namespace NT.QAMS.Application.Abstractions;

/// <summary>
/// Persistence port for application handlers. Grows one DbSet per aggregate as
/// modules land; Infrastructure implements it. Handlers call SaveChangesAsync —
/// the interceptors (audit stamp, tenant stamp, outbox) run inside it.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<UserAccount> Users { get; }
    DbSet<PasswordHistoryEntry> PasswordHistory { get; }
    DbSet<Nonconformance> Nonconformances { get; }
    DbSet<Complaint> Complaints { get; }
    DbSet<QualityObjective> QualityObjectives { get; }
    DbSet<FeedbackEntry> FeedbackEntries { get; }
    DbSet<ControlledDocument> Documents { get; }
    DbSet<FileReference> Files { get; }
    DbSet<Audit> Audits { get; }
    DbSet<EquipmentItem> EquipmentItems { get; }
    DbSet<ReferenceStandard> ReferenceStandards { get; }
    DbSet<TestAuthorization> TestAuthorizations { get; }
    DbSet<MonitoringPoint> MonitoringPoints { get; }
    DbSet<CompetencyRecord> Competencies { get; }
    DbSet<TrainingAssignment> TrainingAssignments { get; }
    DbSet<RiskItem> Risks { get; }
    DbSet<ConflictDeclaration> ConflictDeclarations { get; }
    DbSet<InterestedParty> InterestedParties { get; }
    DbSet<ContextIssue> ContextIssues { get; }
    DbSet<ChangeRequest> ChangeRequests { get; }
    DbSet<ManagementReview> ManagementReviews { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierEvaluation> SupplierEvaluations { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Department> Departments { get; }
    DbSet<TestCatalogItem> TestCatalogItems { get; }
    DbSet<LovEntry> LovEntries { get; }
    DbSet<NotificationRule> NotificationRules { get; }
    DbSet<NotificationDispatch> NotificationDispatches { get; }
    DbSet<QcProfile> QcProfiles { get; }
    DbSet<QcRun> QcRuns { get; }
    DbSet<ValidationStudy> ValidationStudies { get; }
    DbSet<MethodComparisonStudy> MethodComparisons { get; }
    DbSet<LinearityStudy> LinearityStudies { get; }
    DbSet<DetectionLimitStudy> DetectionLimitStudies { get; }
    DbSet<ReferenceIntervalStudy> ReferenceIntervalStudies { get; }
    DbSet<SigmaAssessment> SigmaAssessments { get; }
    DbSet<PrecisionStudy> PrecisionStudies { get; }
    DbSet<PtEnrollment> PtEnrollments { get; }
    DbSet<PtPlan> PtPlans { get; }
    DbSet<AuditTrailReview> AuditTrailReviews { get; }
    DbSet<UncertaintyBudget> UncertaintyBudgets { get; }
    DbSet<ArchiveEntry> ArchiveEntries { get; }
    DbSet<SlaDefinition> SlaDefinitions { get; }
    DbSet<WorkTask> WorkTasks { get; }
    DbSet<EscalationTimer> EscalationTimers { get; }
    DbSet<KpiSnapshot> KpiSnapshots { get; }
    DbSet<FieldChangeRecord> FieldChanges { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
