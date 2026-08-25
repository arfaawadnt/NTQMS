using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Domain.Integration;

/// <summary>The source/target system an interface connects to.</summary>
public enum InterfaceSystem { His, Lis, Pharmacy, Hr, Other }

/// <summary>How the interface exchanges data.</summary>
public enum InterfaceProtocol { Hl7V2, FhirR4, FileExtract, DbExtract }

/// <summary>Lifecycle of an interface endpoint.</summary>
public enum EndpointStatus { Active, Suspended }

/// <summary>
/// A configured integration endpoint (HQMS M24): a named interface to a HIS/LIS/pharmacy/HR
/// system over HL7 v2, FHIR R4 or a file/DB extract. Holds the health signal — last message,
/// last error, consecutive failures — that drives the interface-monitoring dashboard and
/// failure alerting. The wire-protocol adapter feeds messages into the inbox and reports
/// success/failure back here.
/// </summary>
public sealed class IntegrationEndpoint : AggregateRoot, ITenantScoped
{
    /// <summary>Consecutive failures at or above which the endpoint is considered unhealthy (alert).</summary>
    public const int UnhealthyThreshold = 3;

    private IntegrationEndpoint() { Name = null!; }

    public Guid TenantId { get; set; }
    public string Name { get; private set; }
    public InterfaceSystem System { get; private set; }
    public InterfaceProtocol Protocol { get; private set; }
    public EndpointStatus Status { get; private set; }
    public DateTimeOffset? LastMessageAtUtc { get; private set; }
    public DateTimeOffset? LastErrorAtUtc { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    /// <summary>True when the endpoint is active and not in a failure streak.</summary>
    public bool IsHealthy => Status == EndpointStatus.Active && ConsecutiveFailures < UnhealthyThreshold;

    public static IntegrationEndpoint Register(string name, InterfaceSystem system, InterfaceProtocol protocol)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("INT-001", "An endpoint name is required.");
        }

        return new IntegrationEndpoint
        {
            Name = name.Trim(),
            System = system,
            Protocol = protocol,
            Status = EndpointStatus.Active,
        };
    }

    public void Suspend()
    {
        if (Status == EndpointStatus.Suspended)
        {
            throw new InvalidStateTransitionException("INT-002", "The endpoint is already suspended.");
        }

        Status = EndpointStatus.Suspended;
    }

    public void Resume()
    {
        if (Status == EndpointStatus.Active)
        {
            throw new InvalidStateTransitionException("INT-003", "The endpoint is already active.");
        }

        Status = EndpointStatus.Active;
        ConsecutiveFailures = 0;
    }

    /// <summary>Records that a message was ingested successfully (clears the failure streak).</summary>
    public void RecordSuccess(DateTimeOffset at)
    {
        LastMessageAtUtc = at;
        ConsecutiveFailures = 0;
    }

    /// <summary>Records an ingestion failure; raises an alert event when the streak crosses the threshold.</summary>
    public void RecordFailure(DateTimeOffset at)
    {
        LastMessageAtUtc = at;
        LastErrorAtUtc = at;
        ConsecutiveFailures++;
        if (ConsecutiveFailures == UnhealthyThreshold)
        {
            Raise(new InterfaceUnhealthy(Id, Name, System.ToString(), ConsecutiveFailures));
        }
    }
}

public sealed record InterfaceUnhealthy(Guid EndpointId, string Name, string System, int ConsecutiveFailures) : DomainEvent;
