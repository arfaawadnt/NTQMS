using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Reporting;

/// <summary>
/// The quality sub-systems that contribute to the composite Quality Health Score.
/// Each maps to one section of the Quality Statistics report and to one ISO/IEC
/// 17025 §8.9.2 management-review input, so the score and the review pack are
/// derived from the same components rather than two parallel definitions.
/// </summary>
public enum QualityHealthCategory
{
    DocumentControl,
    NonconformanceCapa,
    Complaints,
    InternalAudit,
    Equipment,
    Competency,
    ProficiencyTesting,
    SupplierQuality,
    Risk,
}

/// <summary>
/// One category's relative contribution to the composite score. The weight is
/// <em>relative</em>, not a percentage: the score is a weighted mean over the
/// categories that actually contributed, so weights need not sum to any total and
/// a category can be excluded outright by setting it to zero.
/// </summary>
public sealed class QualityHealthWeight
{
    private QualityHealthWeight() { }

    internal QualityHealthWeight(QualityHealthCategory category, int weight)
    {
        Category = category;
        Weight = weight;
    }

    public QualityHealthCategory Category { get; private set; }

    /// <summary>Relative weight, 0–100. Zero excludes the category from the score.</summary>
    public int Weight { get; private set; }

    internal void Set(int weight) => Weight = weight;
}

/// <summary>
/// Per-tenant definition of how the composite Quality Health Score is calculated
/// (one profile per tenant). The score is a governance figure reported to
/// management review, so <em>how</em> it is computed is itself controlled
/// information: every change raises <see cref="QualityHealthWeightsChanged"/> and
/// therefore lands in the tamper-evident audit trail with its reason.
///
/// The profile deliberately holds only the weighting. The achieved score per
/// category is computed from live operational rows at request time and is never
/// stored here — a stored score would be a second source of truth that could
/// silently diverge from the records it claims to summarise.
/// </summary>
public sealed class QualityHealthProfile : AggregateRoot, ITenantScoped
{
    /// <summary>Applied to every category when a tenant has not tuned its weighting.</summary>
    public const int DefaultWeight = 10;

    /// <summary>Upper bound for a single category's relative weight.</summary>
    public const int MaxWeight = 100;

    private readonly List<QualityHealthWeight> _weights = [];

    private QualityHealthProfile() { }

    public Guid TenantId { get; set; }

    public IReadOnlyList<QualityHealthWeight> Weights => _weights;

    /// <summary>
    /// Creates the profile with every category equally weighted. Equal weighting is
    /// the deliberate default: any other starting spread would assert a priority
    /// ordering across quality sub-systems that this system has no basis to claim
    /// on a tenant's behalf.
    /// </summary>
    public static QualityHealthProfile CreateDefault()
    {
        var profile = new QualityHealthProfile();
        foreach (var category in Enum.GetValues<QualityHealthCategory>())
        {
            profile._weights.Add(new QualityHealthWeight(category, DefaultWeight));
        }

        return profile;
    }

    /// <summary>
    /// Replaces the weighting. Every category must be supplied — a partial update
    /// would leave the caller guessing what the omitted categories now weigh — and
    /// at least one must be non-zero, because an all-zero profile would make the
    /// composite score undefined while still appearing to report one.
    /// </summary>
    public void ReplaceWeights(IReadOnlyDictionary<QualityHealthCategory, int> weights, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "QHP-001", "A reason is required when changing how the quality health score is calculated.");
        }

        var categories = Enum.GetValues<QualityHealthCategory>();
        var missing = categories.Where(c => !weights.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException(
                "QHP-002", $"A weight is required for every category; missing: {string.Join(", ", missing)}.");
        }

        var outOfRange = weights.Where(w => w.Value is < 0 or > MaxWeight).ToList();
        if (outOfRange.Count > 0)
        {
            throw new DomainException(
                "QHP-003", $"A weight must be between 0 and {MaxWeight}.");
        }

        if (categories.All(c => weights[c] == 0))
        {
            throw new DomainException(
                "QHP-004", "At least one category must carry a non-zero weight.");
        }

        var changed = new List<string>();
        foreach (var category in categories)
        {
            var existing = _weights.SingleOrDefault(w => w.Category == category);
            var next = weights[category];
            if (existing is null)
            {
                _weights.Add(new QualityHealthWeight(category, next));
                changed.Add($"{category}:→{next}");
            }
            else if (existing.Weight != next)
            {
                changed.Add($"{category}:{existing.Weight}→{next}");
                existing.Set(next);
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        Raise(new QualityHealthWeightsChanged([.. changed], reason.Trim()));
    }

    /// <summary>The weight for a category, or zero when the profile predates it.</summary>
    public int WeightFor(QualityHealthCategory category) =>
        _weights.SingleOrDefault(w => w.Category == category)?.Weight ?? 0;
}

/// <summary>
/// The scoring definition changed. <paramref name="Changes"/> holds one
/// <c>Category:old→new</c> entry per altered weight so the trail records what the
/// score meant before and after, not merely that something was edited.
/// </summary>
public sealed record QualityHealthWeightsChanged(string[] Changes, string Reason) : DomainEvent;
