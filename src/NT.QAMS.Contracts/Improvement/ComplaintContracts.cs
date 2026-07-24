namespace NT.QAMS.Contracts.Improvement;

public sealed record LogComplaintRequest(
    string Channel, string ComplainantName, string? ComplainantContact,
    bool Confidential, string Subject, string Description);

public sealed record ValidateComplaintRequest(bool Justified, string Reason);
public sealed record LogComplaintOutcomeRequest(string Outcome);
public sealed record ResolveComplaintRequest(string Resolution);

public sealed record ComplaintListItemDto(
    Guid Id, string ComplaintRef, string Subject, string Channel, string Status,
    bool Confidential, string ComplainantName, DateTimeOffset LoggedAtUtc);

public sealed record ComplaintDetailDto(
    Guid Id, string ComplaintRef, string Channel, string ComplainantName,
    string? ComplainantContact, bool Confidential, string Subject, string Description,
    string Status, DateTimeOffset LoggedAtUtc, DateTimeOffset? AcknowledgedAtUtc,
    string? ValidationVerdict, string? InvestigationOutcome, string? Resolution, Guid? LinkedNcId);
