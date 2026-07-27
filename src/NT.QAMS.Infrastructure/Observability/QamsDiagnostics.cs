using System.Diagnostics;

namespace NT.QAMS.Infrastructure.Observability;

/// <summary>
/// OBS-002: the Infrastructure layer's tracing sources. Background work spans
/// come from here — the outbox processor parents each event span on the
/// trace that WROTE the row (persisted traceparent), so one trace reads
/// HTTP → MediatR → EF → Outbox across the async boundary. The WebApi
/// composition root subscribes OpenTelemetry to these names.
/// </summary>
public static class QamsDiagnostics
{
    /// <summary>Spans for outbox event delivery (parented on the writing trace).</summary>
    public const string OutboxSourceName = "NT.QAMS.Outbox";

    /// <summary>Spans for the recurring jobs (compliance sweep, KPI snapshot).</summary>
    public const string JobsSourceName = "NT.QAMS.Jobs";

    public static readonly ActivitySource Outbox = new(OutboxSourceName);
    public static readonly ActivitySource Jobs = new(JobsSourceName);
}
