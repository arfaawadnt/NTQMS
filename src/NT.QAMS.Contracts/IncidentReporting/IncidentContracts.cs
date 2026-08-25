namespace NT.QAMS.Contracts.IncidentReporting;

/// <summary>Submits an attributed incident report.</summary>
public sealed record ReportIncidentRequest(
    string Title, string Description, string Category, string HarmGrade, string Channel,
    DateTimeOffset OccurredAtUtc, string? Location = null,
    Guid? BranchId = null, Guid? DepartmentId = null);

/// <summary>Submits an incident report with the reporter's identity suppressed.</summary>
public sealed record ReportAnonymousIncidentRequest(
    string Title, string Description, string Category, string HarmGrade, string Channel,
    DateTimeOffset OccurredAtUtc, string? Location = null,
    Guid? BranchId = null, Guid? DepartmentId = null);

/// <summary>The one-time follow-up reference issued to an anonymous reporter. Shown once, never recoverable.</summary>
public sealed record AnonymousIncidentReceipt(Guid Id, string IncidentRef, string FollowUpReference);

public sealed record TriageIncidentRequest(Guid AssigneeId, string Category);

public sealed record RejectIncidentRequest(string Reason);

public sealed record StartInvestigationRequest(Guid InvestigatorId);

public sealed record AddContributingFactorRequest(string Category, string Description);

public sealed record AddTimelineEntryRequest(DateTimeOffset OccurredAtUtc, string Note);

public sealed record RecordInvestigationSummaryRequest(string Summary);

/// <summary>Closing an incident is a Part 11 signing ceremony (account password + signature PIN).</summary>
public sealed record CloseIncidentRequest(string ClosureSummary, string Password, string Pin);

/// <summary>Declaring a sentinel event is a Part 11 signing ceremony (account password + signature PIN).</summary>
public sealed record DeclareSentinelRequest(string Password, string Pin);

public sealed record ContributingFactorDto(Guid Id, string Category, string Description);

public sealed record IncidentTimelineEntryDto(Guid Id, DateTimeOffset OccurredAtUtc, string Note, Guid RecordedBy);

public sealed record IncidentListItemDto(
    Guid Id, string IncidentRef, string Title, string Status, string Category, string HarmGrade,
    bool IsSentinel, bool IsAnonymous, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc,
    Guid? BranchId = null, Guid? DepartmentId = null);

public sealed record IncidentDetailDto(
    Guid Id, string IncidentRef, string Title, string Description, string Status,
    string Category, string? Location, string HarmGrade, string Channel,
    bool IsSentinel, DateTimeOffset? SentinelDeclaredAtUtc,
    bool IsAnonymous, Guid? ReportedBy, Guid? AssignedTo, Guid? InvestigatorId,
    string? InvestigationSummary, string? RejectionReason, string? ClosureSummary,
    Guid? CorrectiveActionNcId,
    DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ContributingFactorDto> ContributingFactors,
    IReadOnlyList<IncidentTimelineEntryDto> Timeline);

/// <summary>Status view returned to an anonymous reporter tracking their report by reference.</summary>
public sealed record IncidentTrackingDto(string IncidentRef, string Status, bool IsSentinel);
