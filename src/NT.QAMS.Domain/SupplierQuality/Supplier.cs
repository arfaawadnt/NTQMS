using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.SupplierQuality;

public enum SupplierStatus { PendingEvaluation, Approved, Suspended }

/// <summary>Lifecycle of a supplier contract / SLA (HQMS M16).</summary>
public enum ContractStatus { Active, Terminated }

/// <summary>Lifecycle of a supplier corrective-action request (HQMS M16).</summary>
public enum SupplierCarStatus { Open, ResponseReceived, Closed }

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
/// A supplier contract / SLA (HQMS M16): the agreement governing an outsourced service or supply,
/// with its term and a summary of service-level commitments. Active until terminated; expired when
/// past its end date.
/// </summary>
public sealed class SupplierContract : Entity
{
    internal SupplierContract(string contractRef, string title, DateOnly startDate, DateOnly endDate, string? slaSummary)
    {
        ContractRef = contractRef;
        Title = title;
        StartDate = startDate;
        EndDate = endDate;
        SlaSummary = slaSummary;
        Status = ContractStatus.Active;
    }

    private SupplierContract() { ContractRef = null!; Title = null!; }

    public string ContractRef { get; private set; }
    public string Title { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? SlaSummary { get; private set; }
    public ContractStatus Status { get; private set; }
    public string? TerminationReason { get; private set; }

    public bool IsExpired(DateOnly asOf) => Status == ContractStatus.Active && EndDate < asOf;

    internal void Terminate(string reason) { Status = ContractStatus.Terminated; TerminationReason = reason; }
}

/// <summary>
/// A corrective-action request raised against a supplier (HQMS M16): the formal loop that a
/// supplier non-conformance is worked through — raised, the supplier's response recorded, then
/// closed with a verification of whether it was effective.
/// </summary>
public sealed class SupplierCar : Entity
{
    internal SupplierCar(string description, DateOnly raisedOn, DateOnly? dueDate)
    {
        Description = description;
        RaisedOn = raisedOn;
        DueDate = dueDate;
        Status = SupplierCarStatus.Open;
    }

    private SupplierCar() { Description = null!; }

    public string Description { get; private set; }
    public DateOnly RaisedOn { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public SupplierCarStatus Status { get; private set; }
    public string? ResponseNote { get; private set; }
    public DateOnly? ResponseOn { get; private set; }
    public bool? Effective { get; private set; }
    public string? ClosureNote { get; private set; }

    public bool IsOverdue(DateOnly asOf) => Status != SupplierCarStatus.Closed && DueDate is { } due && asOf > due;

    internal void RecordResponse(string note, DateOnly on)
    {
        // N-13: a response cannot predate the CAR being raised.
        if (on < RaisedOn)
        {
            throw new DomainException("SUP-CAR-010", "The response date cannot precede the date the CAR was raised.");
        }

        Status = SupplierCarStatus.ResponseReceived;
        ResponseNote = note;
        ResponseOn = on;
    }
    internal void Close(bool effective, string closureNote) { Status = SupplierCarStatus.Closed; Effective = effective; ClosureNote = closureNote; }
}

/// <summary>
/// Supplier approval lifecycle. SoD rule 5 (SOD-SUP-001): the approver cannot be
/// the user who registered the supplier. Certificate expiry auto-suspends via
/// the sweep (proposal method — the aggregate decides).
/// </summary>
public sealed class Supplier : AggregateRoot, ITenantScoped, IAllocatable
{
    private readonly List<CertificateRecord> _certificates = [];
    private readonly List<SupplierContract> _contracts = [];
    private readonly List<SupplierCar> _cars = [];

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

    /// <summary>Whether this supplier provides an outsourced clinical service (ref lab, radiology, dialysis…).</summary>
    public bool IsOutsourcedClinicalService { get; private set; }

    /// <summary>The scope of the outsourced clinical service, when applicable.</summary>
    public string? ServiceScope { get; private set; }

    public IReadOnlyList<CertificateRecord> Certificates => _certificates.AsReadOnly();
    public IReadOnlyList<SupplierContract> Contracts => _contracts.AsReadOnly();
    public IReadOnlyList<SupplierCar> Cars => _cars.AsReadOnly();

    /// <summary>Open (not-closed) corrective-action requests — the supplier-quality backlog.</summary>
    public int OpenCarCount => _cars.Count(c => c.Status != SupplierCarStatus.Closed);

    public static Supplier Register(
        string supplierRef, string name, string supplierType, Guid registeredBy,
        bool isOutsourcedClinicalService = false, string? serviceScope = null)
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
            IsOutsourcedClinicalService = isOutsourcedClinicalService,
            ServiceScope = string.IsNullOrWhiteSpace(serviceScope) ? null : serviceScope.Trim(),
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

    // ── Contract / SLA register (HQMS M16) ──────────────────────────────────────

    public Guid AddContract(string contractRef, string title, DateOnly startDate, DateOnly endDate, string? slaSummary)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("SUP-030", "A contract title is required.");
        }

        if (endDate < startDate)
        {
            throw new DomainException("SUP-031", "The contract end date cannot precede its start.");
        }

        var contract = new SupplierContract(
            contractRef, title.Trim(), startDate, endDate, string.IsNullOrWhiteSpace(slaSummary) ? null : slaSummary.Trim());
        _contracts.Add(contract);
        return contract.Id;
    }

    public void TerminateContract(Guid contractId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("SUP-032", "A termination reason is required.");
        }

        var contract = _contracts.FirstOrDefault(c => c.Id == contractId)
            ?? throw new DomainException("SUP-033", "Contract not found.");
        if (contract.Status == ContractStatus.Terminated)
        {
            throw new InvalidStateTransitionException("SUP-034", "The contract is already terminated.");
        }

        contract.Terminate(reason.Trim());
    }

    // ── Corrective-action requests (HQMS M16) ───────────────────────────────────

    public Guid RaiseCar(string description, DateOnly raisedOn, DateOnly? dueDate)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("SUP-040", "A CAR description is required.");
        }

        var car = new SupplierCar(description.Trim(), raisedOn, dueDate);
        _cars.Add(car);
        return car.Id;
    }

    public void RecordCarResponse(Guid carId, string note, DateOnly on)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException("SUP-041", "A response note is required.");
        }

        var car = LoadCar(carId);
        if (car.Status != SupplierCarStatus.Open)
        {
            throw new InvalidStateTransitionException("SUP-042", $"A CAR in state {car.Status} cannot receive a response.");
        }

        car.RecordResponse(note.Trim(), on);
    }

    public void CloseCar(Guid carId, bool effective, string closureNote)
    {
        if (string.IsNullOrWhiteSpace(closureNote))
        {
            throw new DomainException("SUP-043", "A closure note is required.");
        }

        var car = LoadCar(carId);
        if (car.Status != SupplierCarStatus.ResponseReceived)
        {
            throw new InvalidStateTransitionException("SUP-044", "A CAR must have a recorded response before it is closed.");
        }

        car.Close(effective, closureNote.Trim());
    }

    private SupplierCar LoadCar(Guid carId) =>
        _cars.FirstOrDefault(c => c.Id == carId) ?? throw new DomainException("SUP-045", "CAR not found.");
}

/// <summary>
/// Periodic weighted evaluation — separate aggregate: evaluations accrete forever
/// and are historical records of fact (the weighted total is the score of record).
/// </summary>
public sealed class SupplierEvaluation : AggregateRoot, ITenantScoped
{
    private SupplierEvaluation()
    {
        Criteria = null!;
    }

    public Guid TenantId { get; set; }
    public Guid SupplierId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    /// <summary>Criterion name → (weight, score) captured as a JSON document; the total is the record.</summary>
    public string Criteria { get; private set; }
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
            Criteria = System.Text.Json.JsonSerializer.Serialize(
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
