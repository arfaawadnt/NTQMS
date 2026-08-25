using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Integration;

/// <summary>Processing state of an inbound integration message.</summary>
public enum MessageStatus { Received, Processed, Failed }

/// <summary>
/// One inbound message in the integration inbox (HQMS M24). Every message lands here — raw
/// payload plus a deduplication key — before it is processed, so redelivery is idempotent
/// and a failed message can be inspected and replayed. The dedup key is unique per
/// (tenant, endpoint); the adapter derives it from the message control id.
/// </summary>
public sealed class IntegrationMessage : AggregateRoot, ITenantScoped
{
    private IntegrationMessage()
    {
        DedupKey = null!;
        MessageType = null!;
        RawPayload = null!;
    }

    public Guid TenantId { get; set; }
    public Guid EndpointId { get; private set; }
    public string DedupKey { get; private set; }

    /// <summary>Message type/trigger (e.g. "ADT^A01", "ORU^R01").</summary>
    public string MessageType { get; private set; }

    /// <summary>The raw message as received (HL7 pipe-delimited, FHIR JSON, or extract row).</summary>
    public string RawPayload { get; private set; }

    public MessageStatus Status { get; private set; }
    public string? ErrorDetail { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public static IntegrationMessage Receive(
        Guid endpointId, string dedupKey, string messageType, string rawPayload, DateTimeOffset at)
    {
        if (endpointId == Guid.Empty)
        {
            throw new DomainException("MSG-001", "An endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(dedupKey))
        {
            throw new DomainException("MSG-002", "A deduplication key is required.");
        }

        return new IntegrationMessage
        {
            EndpointId = endpointId,
            DedupKey = dedupKey.Trim(),
            MessageType = string.IsNullOrWhiteSpace(messageType) ? "UNKNOWN" : messageType.Trim(),
            RawPayload = rawPayload ?? string.Empty,
            Status = MessageStatus.Received,
            ReceivedAtUtc = at,
        };
    }

    public void MarkProcessed(DateTimeOffset at)
    {
        Status = MessageStatus.Processed;
        ProcessedAtUtc = at;
        ErrorDetail = null;
    }

    public void MarkFailed(string error, DateTimeOffset at)
    {
        Status = MessageStatus.Failed;
        ProcessedAtUtc = at;
        ErrorDetail = string.IsNullOrWhiteSpace(error) ? "Unspecified error." : error.Trim();
    }
}
