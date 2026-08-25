namespace NT.QAMS.Contracts.Integration;

public sealed record RegisterEndpointRequest(string Name, string System, string Protocol);

/// <summary>
/// A canonical ADT event handed to the hub by a protocol adapter (HL7 v2 / FHIR / extract).
/// The adapter parses the wire format; the hub records it and updates the patient-stay
/// projection.
/// </summary>
public sealed record IngestAdtEventRequest(
    string DedupKey, string MessageType, string RawPayload, string EventType,
    string PatientRef, string EncounterRef, string Unit, Guid? DepartmentId, DateTimeOffset EventAtUtc);

public sealed record IngestResultDto(Guid MessageId, string Status, string? Error);

public sealed record EndpointListItemDto(
    Guid Id, string Name, string System, string Protocol, string Status, bool Healthy,
    DateTimeOffset? LastMessageAtUtc, DateTimeOffset? LastErrorAtUtc, int ConsecutiveFailures,
    int Received, int Processed, int Failed);

public sealed record IntegrationMessageDto(
    Guid Id, Guid EndpointId, string DedupKey, string MessageType, string Status,
    string? ErrorDetail, DateTimeOffset ReceivedAtUtc, DateTimeOffset? ProcessedAtUtc);

/// <summary>Reconciliation for one endpoint: message counts by processing state.</summary>
public sealed record ReconciliationDto(Guid EndpointId, string Name, int Received, int Processed, int Failed);

/// <summary>Live census projection derived from ADT: currently-admitted stays and patient-days in a window.</summary>
public sealed record PatientCensusDto(int ActiveStays, int PatientDaysWindow, DateTimeOffset AsOfUtc, DateTimeOffset FromUtc);
