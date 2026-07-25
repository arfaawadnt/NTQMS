using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.AnalyticalQuality;

public enum LotComparisonState { DataEntry, Calculated, SignedOff }

/// <summary>One sample measured on both the current and the new lot.</summary>
public sealed class LotSamplePair : Entity
{
    internal LotSamplePair(decimal currentLotValue, decimal newLotValue, string? sampleId)
    {
        CurrentLotValue = currentLotValue;
        NewLotValue = newLotValue;
        SampleId = sampleId;
    }

    private LotSamplePair() { }

    public decimal CurrentLotValue { get; private set; }
    public decimal NewLotValue { get; private set; }
    public string? SampleId { get; private set; }
}

/// <summary>
/// Reagent/control lot-to-lot comparison: shared samples measured on the current
/// and the new lot. The mean percentage bias of the new lot relative to the
/// current lot is assessed against an allowable limit; within it, the new lot is
/// accepted for cross-over. Pairs are immutable; the verdict is derivable-only;
/// sign-off freezes the study.
/// </summary>
public sealed class LotComparisonStudy : AggregateRoot, ITenantScoped
{
    public const int MinimumPairs = 3;

    private readonly List<LotSamplePair> _pairs = [];

    private LotComparisonStudy()
    {
        StudyRef = null!;
        Analyte = null!;
        Unit = null!;
        CurrentLot = null!;
        NewLot = null!;
    }

    public Guid TenantId { get; set; }
    public string StudyRef { get; private set; }
    public string Analyte { get; private set; }
    public string Unit { get; private set; }
    public string CurrentLot { get; private set; }
    public string NewLot { get; private set; }
    /// <summary>Maximum acceptable mean bias between lots, as a percentage.</summary>
    public decimal AllowableBiasPct { get; private set; }
    public LotComparisonState State { get; private set; }

    // Derived (null until Calculate).
    public int? PairCount { get; private set; }
    public decimal? MeanCurrent { get; private set; }
    public decimal? MeanNew { get; private set; }
    public decimal? MeanBiasPct { get; private set; }
    public bool? Passes { get; private set; }

    public Guid? SignedOffBy { get; private set; }
    public DateTimeOffset? SignedOffAtUtc { get; private set; }

    public IReadOnlyList<LotSamplePair> Pairs => _pairs.AsReadOnly();

    public static LotComparisonStudy Configure(
        string studyRef, string analyte, string unit, string currentLot, string newLot, decimal allowableBiasPct)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            throw new DomainException("LOT-001", "An analyte is required.");
        }

        if (string.IsNullOrWhiteSpace(currentLot) || string.IsNullOrWhiteSpace(newLot))
        {
            throw new DomainException("LOT-002", "Both the current and new lot identifiers are required.");
        }

        if (allowableBiasPct is <= 0m or > 50m)
        {
            throw new DomainException("LOT-003", "The allowable bias must be a positive percentage (at most 50%).");
        }

        return new LotComparisonStudy
        {
            StudyRef = studyRef,
            Analyte = analyte.Trim(),
            Unit = unit?.Trim() ?? string.Empty,
            CurrentLot = currentLot.Trim(),
            NewLot = newLot.Trim(),
            AllowableBiasPct = allowableBiasPct,
            State = LotComparisonState.DataEntry,
        };
    }

    public Guid AddPair(decimal currentLotValue, decimal newLotValue, string? sampleId)
    {
        RequireEditable();
        if (currentLotValue <= 0m || newLotValue <= 0m)
        {
            throw new DomainException("LOT-004", "Measured values must be positive.");
        }

        var pair = new LotSamplePair(currentLotValue, newLotValue,
            string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim());
        _pairs.Add(pair);
        Invalidate();
        return pair.Id;
    }

    public void RemovePair(Guid pairId)
    {
        RequireEditable();
        var pair = _pairs.FirstOrDefault(p => p.Id == pairId)
            ?? throw new DomainException("LOT-404", "Sample pair not found.");
        _pairs.Remove(pair);
        Invalidate();
    }

    public void Calculate()
    {
        RequireEditable();
        if (_pairs.Count < MinimumPairs)
        {
            throw new DomainException("LOT-010", $"At least {MinimumPairs} paired samples are required.");
        }

        var meanCurrent = _pairs.Average(p => p.CurrentLotValue);
        var meanNew = _pairs.Average(p => p.NewLotValue);
        if (meanCurrent == 0m)
        {
            throw new DomainException("LOT-011", "The current-lot mean is zero — a percentage bias cannot be computed.");
        }

        MeanCurrent = Math.Round(meanCurrent, 4);
        MeanNew = Math.Round(meanNew, 4);
        MeanBiasPct = Math.Round((meanNew - meanCurrent) / meanCurrent * 100m, 4);
        Passes = Math.Abs(MeanBiasPct.Value) <= AllowableBiasPct;
        PairCount = _pairs.Count;
        State = LotComparisonState.Calculated;
    }

    public void SignOff(Guid actorId, DateTimeOffset at)
    {
        if (State != LotComparisonState.Calculated)
        {
            throw new InvalidStateTransitionException(
                "LOT-012", $"Only a calculated study can be signed off (current: {State}).");
        }

        State = LotComparisonState.SignedOff;
        SignedOffBy = actorId;
        SignedOffAtUtc = at;
        Raise(new LotComparisonSignedOff(Id, StudyRef, Analyte, Passes!.Value, TenantId));
    }

    private void Invalidate()
    {
        PairCount = null;
        MeanCurrent = MeanNew = MeanBiasPct = null;
        Passes = null;
        if (State == LotComparisonState.Calculated)
        {
            State = LotComparisonState.DataEntry;
        }
    }

    private void RequireEditable()
    {
        if (State == LotComparisonState.SignedOff)
        {
            throw new InvalidStateTransitionException("LOT-013", "A signed-off study is immutable.");
        }
    }
}

public sealed record LotComparisonSignedOff(
    Guid StudyId, string StudyRef, string Analyte, bool Passes, Guid TenantId) : DomainEvent;
