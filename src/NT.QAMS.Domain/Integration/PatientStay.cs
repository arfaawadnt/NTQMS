using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Integration;

/// <summary>Whether the patient is currently admitted or has been discharged.</summary>
public enum StayStatus { Admitted, Discharged }

/// <summary>
/// The ADT event kinds this projection understands.
/// </summary>
public enum AdtEventType { Admit, Transfer, Discharge }

/// <summary>
/// A minimal admission/stay projection (HQMS M24, ADR-4): the quality system stores only what
/// its rate calculations need — a pseudonymised patient reference, the encounter, the unit and
/// admit/discharge times — not the clinical record. Patient-days derived from these are the
/// denominators for patient-safety and infection rates (falls per 1,000 patient-days, etc.).
/// </summary>
public sealed class PatientStay : AggregateRoot, ITenantScoped
{
    private PatientStay()
    {
        PatientRef = null!;
        EncounterRef = null!;
        Unit = null!;
    }

    public Guid TenantId { get; set; }

    /// <summary>Pseudonymised patient identifier (never the clinical MRN in clear).</summary>
    public string PatientRef { get; private set; }

    /// <summary>Encounter/visit identifier — the natural key for the stay within a tenant.</summary>
    public string EncounterRef { get; private set; }

    public string Unit { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public DateTimeOffset AdmittedAtUtc { get; private set; }
    public DateTimeOffset? DischargedAtUtc { get; private set; }
    public StayStatus Status { get; private set; }

    public static PatientStay Admit(
        string patientRef, string encounterRef, string unit, Guid? departmentId, DateTimeOffset admittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
        {
            throw new DomainException("STAY-001", "A patient reference is required.");
        }

        if (string.IsNullOrWhiteSpace(encounterRef))
        {
            throw new DomainException("STAY-002", "An encounter reference is required.");
        }

        return new PatientStay
        {
            PatientRef = patientRef.Trim(),
            EncounterRef = encounterRef.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "Unknown" : unit.Trim(),
            DepartmentId = departmentId,
            AdmittedAtUtc = admittedAtUtc,
            Status = StayStatus.Admitted,
        };
    }

    public void Transfer(string unit, Guid? departmentId)
    {
        if (Status != StayStatus.Admitted)
        {
            throw new InvalidStateTransitionException("STAY-010", "Only an admitted stay can be transferred.");
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            Unit = unit.Trim();
        }

        if (departmentId is not null)
        {
            DepartmentId = departmentId;
        }
    }

    public void Discharge(DateTimeOffset at)
    {
        if (Status == StayStatus.Discharged)
        {
            return; // Idempotent: a repeated discharge event is a no-op.
        }

        if (at < AdmittedAtUtc)
        {
            throw new DomainException("STAY-011", "Discharge cannot precede admission.");
        }

        DischargedAtUtc = at;
        Status = StayStatus.Discharged;
    }

    /// <summary>
    /// Patient-days accrued up to <paramref name="asOf"/> (or discharge, if earlier). A stay
    /// on the day of admission counts as at least one patient-day.
    /// </summary>
    public int PatientDays(DateTimeOffset asOf) =>
        WindowedDays.Clipped(AdmittedAtUtc, DischargedAtUtc, AdmittedAtUtc, asOf);

    /// <summary>
    /// Patient-days this stay contributes to the window [<paramref name="from"/>,
    /// <paramref name="asOf"/>] — the canonical rate denominator every module shares.
    /// </summary>
    public int PatientDaysInWindow(DateTimeOffset from, DateTimeOffset asOf) =>
        WindowedDays.Clipped(AdmittedAtUtc, DischargedAtUtc, from, asOf);
}
