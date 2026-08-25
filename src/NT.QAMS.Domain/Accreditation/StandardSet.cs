using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Accreditation;

/// <summary>The accreditation framework a standard set belongs to.</summary>
public enum AccreditationFramework { GAHAR, JCI, ISO9001, ISO15189, Other }

/// <summary>Lifecycle of a standard set edition.</summary>
public enum StandardSetStatus { Draft, Active, Archived }

/// <summary>
/// Self-assessment verdict for a measurable element. NotAssessed and NotApplicable are
/// distinct: the former is an outstanding gap, the latter is deliberately out of scope
/// and excluded from the readiness denominator.
/// </summary>
public enum ComplianceStatus { NotAssessed, Compliant, PartiallyCompliant, NonCompliant, NotApplicable }

/// <summary>
/// A measurable element — the leaf of the standard hierarchy that is actually scored and
/// evidenced. Its chapter and standard grouping are carried as fields (a flattened
/// hierarchy) so the set is a single aggregate rather than a fragile three-level nest.
/// </summary>
public sealed class StandardElement : Entity
{
    internal StandardElement(
        string chapterCode, string chapterTitle, string standardCode, string elementCode,
        string text, int weight)
    {
        ChapterCode = chapterCode;
        ChapterTitle = chapterTitle;
        StandardCode = standardCode;
        ElementCode = elementCode;
        Text = text;
        Weight = weight;
        ComplianceStatus = ComplianceStatus.NotAssessed;
    }

    private StandardElement()
    {
        ChapterCode = null!;
        ChapterTitle = null!;
        StandardCode = null!;
        ElementCode = null!;
        Text = null!;
    }

    public string ChapterCode { get; private set; }
    public string ChapterTitle { get; private set; }
    public string StandardCode { get; private set; }
    public string ElementCode { get; private set; }
    public string Text { get; private set; }

    /// <summary>Relative weight in the readiness calculation (surveyor-critical elements weigh more).</summary>
    public int Weight { get; private set; }

    public ComplianceStatus ComplianceStatus { get; private set; }
    public string? AssessmentNote { get; private set; }
    public Guid? AssessedBy { get; private set; }
    public DateTimeOffset? AssessedAtUtc { get; private set; }

    internal void Assess(ComplianceStatus status, string? note, Guid actor, DateTimeOffset at)
    {
        ComplianceStatus = status;
        AssessmentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AssessedBy = actor;
        AssessedAtUtc = at;
    }
}

/// <summary>
/// A standard set the hospital is accredited against (HQMS M07) — a GAHAR/JCI/ISO edition
/// with its measurable elements. Draft while it is being built, Active once in force, then
/// Archived when superseded. Self-assessment scores live on the elements; a set must have
/// at least one element before it can be activated, and only an active set is assessed.
/// </summary>
public sealed class StandardSet : AggregateRoot, ITenantScoped
{
    private readonly List<StandardElement> _elements = [];

    private StandardSet()
    {
        Name = null!;
        Version = null!;
    }

    public Guid TenantId { get; set; }
    public AccreditationFramework Framework { get; private set; }
    public string Name { get; private set; }
    public string Version { get; private set; }
    public StandardSetStatus Status { get; private set; }

    public IReadOnlyList<StandardElement> Elements => _elements.AsReadOnly();

    public static StandardSet Define(AccreditationFramework framework, string name, string version)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("STD-001", "A standard-set name is required.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new DomainException("STD-002", "A version label is required.");
        }

        return new StandardSet
        {
            Framework = framework,
            Name = name.Trim(),
            Version = version.Trim(),
            Status = StandardSetStatus.Draft,
        };
    }

    public Guid AddElement(
        string chapterCode, string chapterTitle, string standardCode, string elementCode,
        string text, int weight)
    {
        if (Status != StandardSetStatus.Draft)
        {
            throw new InvalidStateTransitionException(
                "STD-010", "Elements can only be added while the set is in draft.");
        }

        if (string.IsNullOrWhiteSpace(elementCode))
        {
            throw new DomainException("STD-011", "An element code is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("STD-012", "Element text is required.");
        }

        if (weight < 1)
        {
            throw new DomainException("STD-013", "Element weight must be at least 1.");
        }

        if (_elements.Any(e => e.ElementCode == elementCode.Trim()))
        {
            throw new DomainException("STD-014", $"Element '{elementCode}' already exists in this set.");
        }

        var element = new StandardElement(
            (chapterCode ?? string.Empty).Trim(), (chapterTitle ?? string.Empty).Trim(),
            (standardCode ?? string.Empty).Trim(), elementCode.Trim(), text.Trim(), weight);
        _elements.Add(element);
        return element.Id;
    }

    public void Activate()
    {
        if (Status != StandardSetStatus.Draft)
        {
            throw new InvalidStateTransitionException("STD-015", $"Cannot activate a set in state {Status}.");
        }

        if (_elements.Count == 0)
        {
            throw new DomainException("STD-016", "A set needs at least one measurable element before activation.");
        }

        Status = StandardSetStatus.Active;
        Raise(new StandardSetActivated(Id, Framework.ToString(), Name, Version, _elements.Count));
    }

    public void Archive()
    {
        if (Status == StandardSetStatus.Archived)
        {
            throw new InvalidStateTransitionException("STD-017", "The set is already archived.");
        }

        Status = StandardSetStatus.Archived;
    }

    /// <summary>Records a self-assessment verdict for one element (active sets only).</summary>
    public void AssessElement(Guid elementId, ComplianceStatus status, string? note, Guid actor, DateTimeOffset at)
    {
        if (Status != StandardSetStatus.Active)
        {
            throw new InvalidStateTransitionException(
                "STD-018", "Only an active set can be self-assessed.");
        }

        var element = _elements.FirstOrDefault(e => e.Id == elementId)
            ?? throw new DomainException("STD-019", "Element not found in this set.");

        element.Assess(status, note, actor, at);
        Raise(new ElementAssessed(Id, elementId, element.ElementCode, status.ToString()));
    }
}

public sealed record StandardSetActivated(
    Guid StandardSetId, string Framework, string Name, string Version, int ElementCount) : DomainEvent;

public sealed record ElementAssessed(
    Guid StandardSetId, Guid ElementId, string ElementCode, string Status) : DomainEvent;
