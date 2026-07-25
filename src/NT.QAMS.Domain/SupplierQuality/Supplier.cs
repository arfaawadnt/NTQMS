using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.SupplierQuality;

public enum SupplierStatus { PendingEvaluation, Approved, Suspended }

public sealed class CertificateRecord : Entity
{
    internal CertificateRecord(string certificateType, DateOnly expiresAt, Guid? fileId)
    {
        CertificateType = certificateType;
        ExpiresAt = expiresAt;
        FileId = fileId;
    }

    private CertificateRecord() { CertificateType = null!; }

    public string CertificateType { get; private set; }
    public DateOnly ExpiresAt { get; private set; }
    public Guid? FileId { get; private set; }
}

/// <summary>
/// Supplier approval lifecycle. SoD rule 5 (SOD-SUP-001): the approver cannot be
/// the user who registered the supplier. Certificate expiry auto-suspends via
/// the sweep (proposal method — the aggregate decides).
/// </summary>
public sealed class Supplier : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<CertificateRecord> _certificates = [];

    private Supplier()
    {
        SupplierRef = null!;
        Name = null!;
        SupplierType = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string SupplierRef { get; private set; }
    public string Name { get; private set; }
    public string SupplierType { get; private set; }
    public Guid RegisteredBy { get; private set; }
    public SupplierStatus Status { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string? SuspensionReason { get; private set; }

    public IReadOnlyList<CertificateRecord> Certificates => _certificates.AsReadOnly();

    public static Supplier Register(string supplierRef, string name, string supplierType, Guid registeredBy)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("SUP-001", "Supplier name is required.");
        }

        return new Supplier
        {
            SupplierRef = supplierRef,
            Name = name.Trim(),
            SupplierType = string.IsNullOrWhiteSpace(supplierType) ? "Reagents" : supplierType.Trim(),
            RegisteredBy = registeredBy,
            Status = SupplierStatus.PendingEvaluation,
        };
    }

    public Guid AddCertificate(string certificateType, DateOnly expiresAt, Guid? fileId)
    {
        if (string.IsNullOrWhiteSpace(certificateType))
        {
            throw new DomainException("SUP-002", "Certificate type is required.");
        }

        var cert = new CertificateRecord(certificateType.Trim(), expiresAt, fileId);
        _certificates.Add(cert);
        return cert.Id;
    }

    public void Approve(Guid actorId)
    {
        if (Status == SupplierStatus.Approved)
        {
            throw new InvalidStateTransitionException("SUP-010", "Supplier is already approved.");
        }

        if (actorId == RegisteredBy)
        {
            throw new DomainException("SOD-SUP-001", "Segregation of duties: the registrant cannot approve their own supplier.");
        }

        Status = SupplierStatus.Approved;
        ApprovedBy = actorId;
        SuspensionReason = null;
        Raise(new SupplierApproved(Id, SupplierRef, Name, actorId, TenantId));
    }

    public void Suspend(string reason)
    {
        if (Status != SupplierStatus.Approved)
        {
            throw new InvalidStateTransitionException("SUP-011", $"Only an approved supplier can be suspended (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("SUP-012", "A suspension reason is required.");
        }

        Status = SupplierStatus.Suspended;
        SuspensionReason = reason.Trim();
        Raise(new SupplierSuspended(Id, SupplierRef, Name, SuspensionReason, TenantId));
    }

    /// <summary>Sweep-proposed: approved supplier with any expired certificate → suspended.</summary>
    public void SuspendIfCertificateExpired(DateOnly asOf)
    {
        if (Status != SupplierStatus.Approved)
        {
            return;
        }

        var expired = _certificates.FirstOrDefault(c => c.ExpiresAt < asOf);
        if (expired is null)
        {
            return;
        }

        Status = SupplierStatus.Suspended;
        SuspensionReason = $"Certificate '{expired.CertificateType}' expired {expired.ExpiresAt:yyyy-MM-dd}.";
        Raise(new SupplierSuspended(Id, SupplierRef, Name, SuspensionReason, TenantId));
    }
}

/// <summary>
/// Periodic weighted evaluation — separate aggregate: evaluations accrete forever
/// and are historical records of fact (the weighted total is the score of record).
/// </summary>
public sealed class SupplierEvaluation : AggregateRoot, ITenantScoped
{
    private SupplierEvaluation()
    {
        CriteriaJson = null!;
    }

    public Guid TenantId { get; set; }
    public Guid SupplierId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    /// <summary>Criterion name → (weight, score) captured as JSON evidence; the total is the record.</summary>
    public string CriteriaJson { get; private set; }
    public decimal WeightedTotal { get; private set; }
    public Guid EvaluatedBy { get; private set; }

    public static SupplierEvaluation Record(
        Guid supplierId, DateOnly periodStart, DateOnly periodEnd,
        IReadOnlyList<(string Criterion, decimal Weight, decimal Score)> criteria,
        Guid evaluatedBy)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException("SUP-020", "Evaluation period end precedes its start.");
        }

        if (criteria is null || criteria.Count == 0)
        {
            throw new DomainException("SUP-021", "At least one evaluation criterion is required.");
        }

        var totalWeight = criteria.Sum(c => c.Weight);
        if (totalWeight <= 0)
        {
            throw new DomainException("SUP-022", "Criterion weights must sum to a positive value.");
        }

        if (criteria.Any(c => c.Score is < 0 or > 100 || c.Weight < 0))
        {
            throw new DomainException("SUP-023", "Scores must be 0-100 and weights non-negative.");
        }

        return new SupplierEvaluation
        {
            SupplierId = supplierId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CriteriaJson = System.Text.Json.JsonSerializer.Serialize(
                criteria.Select(c => new { c.Criterion, c.Weight, c.Score })),
            WeightedTotal = Math.Round(criteria.Sum(c => c.Weight * c.Score) / totalWeight, 2),
            EvaluatedBy = evaluatedBy,
        };
    }
}

public sealed record SupplierApproved(
    Guid SupplierId, string SupplierRef, string Name, Guid ApprovedBy, Guid TenantId) : DomainEvent;

public sealed record SupplierSuspended(
    Guid SupplierId, string SupplierRef, string Name, string Reason, Guid TenantId) : DomainEvent;
