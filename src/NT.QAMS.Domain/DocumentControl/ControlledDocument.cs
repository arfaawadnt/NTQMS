using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.DocumentControl;

public enum DocumentStatus { Draft, Published, Obsolete }

public enum VersionState { Draft, UnderReview, Approved, Published, Obsolete, Rejected }

public enum VersionBump { Major, Minor }

/// <summary>A document version. File bytes live behind FileReference — the aggregate owns control, not content.</summary>
public sealed class DocumentVersion : Entity
{
    internal DocumentVersion(int major, int minor, Guid fileId, string changeSummary, Guid authorId)
    {
        Major = major;
        Minor = minor;
        FileId = fileId;
        ChangeSummary = changeSummary;
        AuthorId = authorId;
        State = VersionState.Draft;
    }

    private DocumentVersion() { ChangeSummary = null!; }

    public int Major { get; private set; }
    public int Minor { get; private set; }
    public Guid FileId { get; private set; }
    public string ChangeSummary { get; private set; }
    public VersionState State { get; internal set; }
    public Guid AuthorId { get; private set; }
    public Guid? RecommendedBy { get; internal set; }
    public DateTimeOffset? RecommendedAtUtc { get; internal set; }
    public Guid? ApprovedBy { get; internal set; }
    public DateTimeOffset? ApprovedAtUtc { get; internal set; }
    public string? RejectionReason { get; internal set; }

    public string VersionLabel => $"{Major}.{Minor}";
}

/// <summary>
/// Controlled document (SOP etc.) — canonical lifecycle Draft → Review → Approved
/// → Published → Obsolete. Invariants: exactly one Published version (publishing
/// v(n) atomically obsoletes v(n−1)); one in-flight version at a time; SoD —
/// the version author can neither recommend nor approve it (SOD-DOC-001/002).
/// Approver/recommender identity + timestamp are recorded per version; the full
/// Part 11 e-signature envelope (PIN ceremony) attaches when Identity Phase 1
/// completes.
/// </summary>
public sealed class ControlledDocument : AggregateRoot, ITenantScoped
{
    private readonly List<DocumentVersion> _versions = [];

    private ControlledDocument()
    {
        Code = null!;
        Title = null!;
        Category = null!;
    }

    public Guid TenantId { get; set; }
    public string Code { get; private set; }
    public string Title { get; private set; }
    public string Category { get; private set; }
    public DocumentStatus Status { get; private set; }

    public IReadOnlyList<DocumentVersion> Versions => _versions.AsReadOnly();

    public DocumentVersion? PublishedVersion => _versions.SingleOrDefault(v => v.State == VersionState.Published);

    public DocumentVersion? InFlightVersion => _versions.SingleOrDefault(v =>
        v.State is VersionState.Draft or VersionState.UnderReview or VersionState.Approved);

    public static ControlledDocument Create(
        string code, string title, string category, Guid fileId, string changeSummary, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("DOC-001", "Document code is required (e.g. SOP-CAL-045).");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("DOC-002", "Document title is required.");
        }

        var doc = new ControlledDocument
        {
            Code = code.Trim().ToUpperInvariant(),
            Title = title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "SOP" : category.Trim(),
            Status = DocumentStatus.Draft,
        };
        doc._versions.Add(new DocumentVersion(1, 0, fileId, changeSummary?.Trim() ?? "Initial issue.", authorId));
        return doc;
    }

    public void SubmitForReview()
    {
        var version = RequireInFlight(VersionState.Draft, "DOC-010", "submit for review");
        version.State = VersionState.UnderReview;
        Raise(new DocumentSubmittedForReview(Id, Code, version.VersionLabel));
    }

    public void Recommend(Guid actorId, DateTimeOffset at)
    {
        var version = RequireInFlight(VersionState.UnderReview, "DOC-011", "recommend");
        if (actorId == version.AuthorId)
        {
            throw new DomainException("SOD-DOC-001", "Segregation of duties: the author cannot review their own document.");
        }

        version.State = VersionState.Approved;
        version.RecommendedBy = actorId;
        version.RecommendedAtUtc = at;
        Raise(new DocumentRecommended(Id, Code, version.VersionLabel, actorId));
    }

    public void RejectVersion(Guid actorId, string reason)
    {
        var version = InFlightVersion
            ?? throw new InvalidStateTransitionException("DOC-012", "No version is awaiting review or approval.");

        if (version.State is not (VersionState.UnderReview or VersionState.Approved))
        {
            throw new InvalidStateTransitionException("DOC-012", $"Cannot reject a version in state {version.State}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("DOC-013", "A rejection reason is required.");
        }

        version.State = VersionState.Draft;
        version.RejectionReason = reason.Trim();
        Raise(new DocumentVersionRejected(Id, Code, version.VersionLabel, actorId, version.RejectionReason));
    }

    public void Publish(Guid actorId, DateTimeOffset at)
    {
        var version = RequireInFlight(VersionState.Approved, "DOC-014", "publish");
        if (actorId == version.AuthorId)
        {
            throw new DomainException("SOD-DOC-002", "Segregation of duties: the author cannot approve their own document.");
        }

        var previous = PublishedVersion;
        if (previous is not null)
        {
            previous.State = VersionState.Obsolete;
            Raise(new DocumentVersionObsoleted(Id, Code, previous.VersionLabel, previous.FileId));
        }

        version.State = VersionState.Published;
        version.ApprovedBy = actorId;
        version.ApprovedAtUtc = at;
        Status = DocumentStatus.Published;
        Raise(new DocumentPublished(Id, Code, Title, version.VersionLabel, actorId));
    }

    public void DraftNewVersion(Guid fileId, string changeSummary, VersionBump bump, Guid authorId)
    {
        if (Status == DocumentStatus.Obsolete)
        {
            throw new InvalidStateTransitionException("DOC-015", "A retired document cannot receive new versions.");
        }

        if (InFlightVersion is not null)
        {
            throw new DomainException("DOC-016", "A version is already in progress; publish or reject it first.");
        }

        var basis = PublishedVersion
            ?? throw new InvalidStateTransitionException("DOC-017", "Only a published document can be revised.");

        var (major, minor) = bump == VersionBump.Major
            ? (basis.Major + 1, 0)
            : (basis.Major, basis.Minor + 1);

        _versions.Add(new DocumentVersion(major, minor, fileId, changeSummary?.Trim() ?? string.Empty, authorId));
    }

    public void Retire(Guid actorId)
    {
        if (Status == DocumentStatus.Obsolete)
        {
            throw new InvalidStateTransitionException("DOC-018", "Document is already obsolete.");
        }

        var published = PublishedVersion;
        if (published is not null)
        {
            published.State = VersionState.Obsolete;
            Raise(new DocumentVersionObsoleted(Id, Code, published.VersionLabel, published.FileId));
        }

        Status = DocumentStatus.Obsolete;
        Raise(new DocumentRetired(Id, Code, actorId));
    }

    private DocumentVersion RequireInFlight(VersionState expected, string code, string action)
    {
        var version = InFlightVersion
            ?? throw new InvalidStateTransitionException(code, $"No version available to {action}.");

        if (version.State != expected)
        {
            throw new InvalidStateTransitionException(
                code, $"Cannot {action} a version in state {version.State}.");
        }

        return version;
    }
}

public sealed record DocumentSubmittedForReview(Guid DocumentId, string Code, string Version) : DomainEvent;
public sealed record DocumentRecommended(Guid DocumentId, string Code, string Version, Guid RecommendedBy) : DomainEvent;
public sealed record DocumentVersionRejected(Guid DocumentId, string Code, string Version, Guid RejectedBy, string Reason) : DomainEvent;
public sealed record DocumentPublished(Guid DocumentId, string Code, string Title, string Version, Guid ApprovedBy) : DomainEvent;
public sealed record DocumentVersionObsoleted(Guid DocumentId, string Code, string Version, Guid FileId) : DomainEvent;
public sealed record DocumentRetired(Guid DocumentId, string Code, Guid RetiredBy) : DomainEvent;
