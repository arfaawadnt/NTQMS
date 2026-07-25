using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum UncertaintyBudgetStatus { Draft, Calculated, Approved }

/// <summary>GUM component evaluation type: A (statistical) or B (other means).</summary>
public enum UncertaintyComponentType { TypeA, TypeB }

/// <summary>
/// One uncertainty contribution, expressed as a RELATIVE standard uncertainty
/// (%). Relative form keeps components combinable across sources (QC
/// repeatability, PT/CRM bias, calibrator, volumetric…).
/// </summary>
public sealed class UncertaintyComponent : Entity
{
    internal UncertaintyComponent(string name, UncertaintyComponentType type, decimal relativeStandardUncertainty, string? source)
    {
        Name = name;
        Type = type;
        RelativeStandardUncertainty = relativeStandardUncertainty;
        Source = source;
    }

    private UncertaintyComponent() { Name = null!; }

    public string Name { get; private set; }
    public UncertaintyComponentType Type { get; private set; }
    /// <summary>u_i as a percentage of the measurand value.</summary>
    public decimal RelativeStandardUncertainty { get; private set; }
    /// <summary>Where the figure came from (QC lot CV, PT bias study, certificate…).</summary>
    public string? Source { get; private set; }
}

/// <summary>
/// Measurement-uncertainty budget per method/analyte/level (ISO 17025 §7.6,
/// ISO 15189 §7.3.4; GUM root-sum-of-squares model). Components accumulate in
/// Draft; Calculate derives the combined standard uncertainty
/// u_c = √Σu_i² and the expanded uncertainty U = k·u_c (relative %); QM
/// approval freezes the budget (an approved budget is evidence — revise by
/// creating a successor). When a target U is set, the verdict compares
/// against it.
/// </summary>
public sealed class UncertaintyBudget : AggregateRoot, ITenantScoped
{
    private readonly List<UncertaintyComponent> _components = [];

    private UncertaintyBudget()
    {
        BudgetRef = null!;
        Analyte = null!;
        Method = null!;
        Unit = null!;
        Level = null!;
    }

    public Guid TenantId { get; set; }
    public string BudgetRef { get; private set; }
    public string Analyte { get; private set; }
    public string Method { get; private set; }
    public string Unit { get; private set; }
    /// <summary>The concentration/activity level the budget applies to (MU is level-dependent).</summary>
    public string Level { get; private set; }
    /// <summary>Coverage factor (k=2 ≈ 95 % for a normal distribution).</summary>
    public decimal CoverageFactor { get; private set; }
    /// <summary>Optional acceptance target for the expanded uncertainty (%).</summary>
    public decimal? TargetExpandedUncertainty { get; private set; }
    public UncertaintyBudgetStatus Status { get; private set; }
    /// <summary>u_c (%) — √Σu_i², set by Calculate.</summary>
    public decimal? CombinedStandardUncertainty { get; private set; }
    /// <summary>U (%) — k·u_c, set by Calculate.</summary>
    public decimal? ExpandedUncertainty { get; private set; }
    /// <summary>U ≤ target (null when no target is set).</summary>
    public bool? MeetsTarget { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public IReadOnlyList<UncertaintyComponent> Components => _components.AsReadOnly();

    public static UncertaintyBudget Create(
        string budgetRef, string analyte, string method, string unit, string level,
        decimal coverageFactor, decimal? targetExpandedUncertainty)
    {
        if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(method))
        {
            throw new DomainException("MU-001", "Analyte and method are required.");
        }

        if (coverageFactor is < 1 or > 4)
        {
            throw new DomainException("MU-002", "The coverage factor k must be between 1 and 4 (k=2 ≈ 95 %).");
        }

        if (targetExpandedUncertainty is <= 0)
        {
            throw new DomainException("MU-003", "The target expanded uncertainty must be positive when set.");
        }

        return new UncertaintyBudget
        {
            BudgetRef = budgetRef,
            Analyte = analyte.Trim(),
            Method = method.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? "%" : unit.Trim(),
            Level = string.IsNullOrWhiteSpace(level) ? "All levels" : level.Trim(),
            CoverageFactor = coverageFactor,
            TargetExpandedUncertainty = targetExpandedUncertainty,
            Status = UncertaintyBudgetStatus.Draft,
        };
    }

    public Guid AddComponent(string name, UncertaintyComponentType type, decimal relativeStandardUncertainty, string? source)
    {
        RequireMutable();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("MU-004", "A component name is required.");
        }

        if (relativeStandardUncertainty < 0)
        {
            throw new DomainException("MU-005", "A standard uncertainty cannot be negative.");
        }

        var component = new UncertaintyComponent(name.Trim(), type, relativeStandardUncertainty, source?.Trim());
        _components.Add(component);
        Status = UncertaintyBudgetStatus.Draft; // Any edit invalidates a prior calculation.
        CombinedStandardUncertainty = null;
        ExpandedUncertainty = null;
        MeetsTarget = null;
        return component.Id;
    }

    public void RemoveComponent(Guid componentId)
    {
        RequireMutable();
        var component = _components.FirstOrDefault(c => c.Id == componentId)
            ?? throw new DomainException("MU-006", "Component not found.");
        _components.Remove(component);
        Status = UncertaintyBudgetStatus.Draft;
        CombinedStandardUncertainty = null;
        ExpandedUncertainty = null;
        MeetsTarget = null;
    }

    /// <summary>Root-sum-of-squares combination and expansion (GUM).</summary>
    public void Calculate()
    {
        RequireMutable();
        if (_components.Count == 0)
        {
            throw new DomainException("MU-007", "At least one uncertainty component is required.");
        }

        var sumOfSquares = _components.Sum(c => (double)c.RelativeStandardUncertainty * (double)c.RelativeStandardUncertainty);
        CombinedStandardUncertainty = Math.Round((decimal)Math.Sqrt(sumOfSquares), 4);
        ExpandedUncertainty = Math.Round(CoverageFactor * CombinedStandardUncertainty.Value, 4);
        MeetsTarget = TargetExpandedUncertainty is { } target ? ExpandedUncertainty <= target : null;
        Status = UncertaintyBudgetStatus.Calculated;
    }

    /// <summary>QM approval freezes the budget as evidence.</summary>
    public void Approve(Guid actorId, DateTimeOffset at)
    {
        if (Status != UncertaintyBudgetStatus.Calculated)
        {
            throw new InvalidStateTransitionException("MU-010", $"Only a calculated budget can be approved (current: {Status}).");
        }

        Status = UncertaintyBudgetStatus.Approved;
        ApprovedBy = actorId;
        ApprovedAtUtc = at;
        Raise(new UncertaintyBudgetApproved(Id, BudgetRef, Analyte, ExpandedUncertainty!.Value, CoverageFactor));
    }

    private void RequireMutable()
    {
        if (Status == UncertaintyBudgetStatus.Approved)
        {
            throw new InvalidStateTransitionException("MU-011", "An approved budget is immutable — create a successor budget to revise.");
        }
    }
}

public sealed record UncertaintyBudgetApproved(
    Guid BudgetId, string BudgetRef, string Analyte, decimal ExpandedUncertainty, decimal CoverageFactor) : DomainEvent;
