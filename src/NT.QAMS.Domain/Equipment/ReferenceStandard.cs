using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Equipment;

public enum ReferenceStandardType { CertifiedReferenceMaterial, ReferenceStandard, WorkingStandard }

public enum ReferenceStandardStatus { Active, Quarantined, Expired, Retired }

/// <summary>
/// Reference standard / certified reference material register (ISO 17025 §6.5):
/// each entry documents the unbroken traceability chain to the SI (certificate,
/// issuer, certified value with its uncertainty) and carries a lifecycle —
/// Active ⇄ Quarantined, Active → Expired (sweep-latched at certificate
/// expiry), and a terminal Retired. Expired or quarantined standards raise an
/// event so dependent measurements can be assessed; they are never deleted.
/// </summary>
public sealed class ReferenceStandard : AggregateRoot, ITenantScoped, IAllocatable
{
    private ReferenceStandard()
    {
        StandardRef = null!;
        Name = null!;
        TraceableTo = null!;
    }

    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string StandardRef { get; private set; }
    public string Name { get; private set; }
    public ReferenceStandardType Type { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? LotNumber { get; private set; }
    public string? CertificateNumber { get; private set; }
    /// <summary>The traceability chain endpoint, e.g. "SI via NIST SRM 917c".</summary>
    public string TraceableTo { get; private set; }
    /// <summary>Certified value incl. unit, e.g. "5.51 mmol/L".</summary>
    public string? CertifiedValue { get; private set; }
    /// <summary>Stated uncertainty of the certified value, e.g. "±0.05 mmol/L (k=2)".</summary>
    public string? UncertaintyStatement { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public ReferenceStandardStatus Status { get; private set; }
    public string? QuarantineReason { get; private set; }

    public static ReferenceStandard Register(
        string standardRef, string name, ReferenceStandardType type, string traceableTo,
        string? manufacturer, string? lotNumber, string? certificateNumber,
        string? certifiedValue, string? uncertaintyStatement,
        DateOnly receivedOn, DateOnly? expiresOn)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("RS-001", "A standard name is required.");
        }

        if (string.IsNullOrWhiteSpace(traceableTo))
        {
            throw new DomainException("RS-002", "The traceability chain (traceable to) is required — an untraceable standard cannot anchor calibrations.");
        }

        if (expiresOn is not null && expiresOn <= receivedOn)
        {
            throw new DomainException("RS-003", "The expiry date must fall after the received date.");
        }

        return new ReferenceStandard
        {
            StandardRef = standardRef,
            Name = name.Trim(),
            Type = type,
            TraceableTo = traceableTo.Trim(),
            Manufacturer = manufacturer?.Trim(),
            LotNumber = lotNumber?.Trim(),
            CertificateNumber = certificateNumber?.Trim(),
            CertifiedValue = certifiedValue?.Trim(),
            UncertaintyStatement = uncertaintyStatement?.Trim(),
            ReceivedOn = receivedOn,
            ExpiresOn = expiresOn,
            Status = ReferenceStandardStatus.Active,
        };
    }

    public void Quarantine(string reason)
    {
        if (Status != ReferenceStandardStatus.Active)
        {
            throw new InvalidStateTransitionException("RS-010", $"Only an active standard can be quarantined (current: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("RS-011", "A quarantine reason is required.");
        }

        Status = ReferenceStandardStatus.Quarantined;
        QuarantineReason = reason.Trim();
        Raise(new ReferenceStandardQuarantined(Id, StandardRef, Name, QuarantineReason, TenantId));
    }

    public void Reactivate(DateOnly asOf)
    {
        if (Status != ReferenceStandardStatus.Quarantined)
        {
            throw new InvalidStateTransitionException("RS-012", $"Only a quarantined standard can be reactivated (current: {Status}).");
        }

        if (ExpiresOn is not null && ExpiresOn <= asOf)
        {
            throw new DomainException("RS-013", "An expired certificate cannot be reactivated — register a replacement standard.");
        }

        Status = ReferenceStandardStatus.Active;
        QuarantineReason = null;
    }

    /// <summary>Sweep-proposed: Active + certificate expiry reached → Expired (latched).</summary>
    public void MarkExpiredIfReached(DateOnly asOf)
    {
        if (Status != ReferenceStandardStatus.Active || ExpiresOn is null || ExpiresOn > asOf)
        {
            return; // Proposal declined — not actually expired.
        }

        Status = ReferenceStandardStatus.Expired;
        Raise(new ReferenceStandardExpired(Id, StandardRef, Name, ExpiresOn.Value, TenantId));
    }

    public void Retire()
    {
        if (Status == ReferenceStandardStatus.Retired)
        {
            throw new InvalidStateTransitionException("RS-014", "The standard is already retired.");
        }

        Status = ReferenceStandardStatus.Retired;
    }
}

public sealed record ReferenceStandardQuarantined(
    Guid StandardId, string StandardRef, string Name, string Reason, Guid TenantId) : DomainEvent;

public sealed record ReferenceStandardExpired(
    Guid StandardId, string StandardRef, string Name, DateOnly ExpiredOn, Guid TenantId) : DomainEvent;
