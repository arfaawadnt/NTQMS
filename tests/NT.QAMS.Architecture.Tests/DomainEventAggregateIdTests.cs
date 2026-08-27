using System.Reflection;
using FluentAssertions;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Architecture.Tests;

/// <summary>
/// Guards the invariant the per-record audit trail depends on: every domain event
/// serialises its <b>aggregate id first</b>.
/// <para>
/// A record's detail-page timeline is built by matching audit-trail entries on the
/// first JSON property of the payload (see <c>ComplianceLedgerStore.GetTrailForRecordAsync</c>).
/// System.Text.Json serialises a positional record in declaration order, so the
/// first primary-constructor parameter becomes the first JSON property. If that
/// parameter is the aggregate id, each entry is attributed to the record that
/// produced it; matching a plain payload substring instead leaked entries that
/// merely <i>reference</i> the id (an actor such as <c>approvedBy</c>, a linked
/// record) into unrelated records' trails.
/// </para>
/// <para>
/// The rule: an event that carries any <see cref="Guid"/> at all must declare one
/// first. Aggregate-less, settings-wide events (no Guid parameter) are exempt —
/// they belong to no record and appear on no per-record timeline.
/// </para>
/// </summary>
public class DomainEventAggregateIdTests
{
    private static readonly Assembly Domain = typeof(NT.QAMS.Domain.Tenancy.Tenant).Assembly;

    public static TheoryData<Type> DomainEvents()
    {
        var data = new TheoryData<Type>();
        foreach (var type in Domain.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(DomainEvent).IsAssignableFrom(t))
                     .OrderBy(t => t.FullName))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void A_domain_event_that_carries_a_guid_declares_its_aggregate_id_first(Type eventType)
    {
        var parameters = PrimaryConstructor(eventType).GetParameters();

        var carriesGuid = parameters.Any(p => p.ParameterType == typeof(Guid) || p.ParameterType == typeof(Guid?));
        if (!carriesGuid)
        {
            return; // Aggregate-less event (e.g. a settings-wide change): not shown on any per-record timeline.
        }

        parameters[0].ParameterType.Should().Be(typeof(Guid),
            $"{eventType.Name} is matched to its record by the first serialised JSON property, so its "
            + "aggregate id must be the first constructor parameter — otherwise that module's per-record "
            + "audit trail silently stops finding its own entries (or matches the wrong record)");
    }

    /// <summary>The positional record's synthesised constructor — everything but the copy constructor.</summary>
    private static ConstructorInfo PrimaryConstructor(Type recordType) =>
        recordType.GetConstructors()
            .Where(c => !(c.GetParameters() is [{ } only] && only.ParameterType == recordType))
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
}
