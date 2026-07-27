using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.MultiTenancy;
using NT.QAMS.SharedKernel.Primitives;
using NT.QAMS.Infrastructure.Persistence.Outbox;

namespace NT.QAMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Drains domain events from tracked aggregates into outbox rows in the SAME
/// transaction as the state change. An event without its change, or a change
/// without its event, is impossible by construction.
/// </summary>
public sealed class OutboxInterceptor(IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Drain(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Drain(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Drain(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var outboxRows = new List<OutboxEvent>();

        foreach (var entry in context.ChangeTracker.Entries<AggregateRoot>())
        {
            if (entry.Entity.DomainEvents.Count == 0)
            {
                continue;
            }

            var tenantId = (entry.Entity as ITenantScoped)?.TenantId;

            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                var eventType = domainEvent.GetType();
                outboxRows.Add(new OutboxEvent
                {
                    Id = domainEvent.EventId,
                    TenantId = tenantId,
                    // Assembly-qualified-lite name: "Namespace.Type, Assembly" —
                    // enough for the processor's type resolution, stable across builds.
                    EventType = $"{eventType.FullName}, {eventType.Assembly.GetName().Name}",
                    Payload = JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
                    OccurredAtUtc = clock.UtcNow,
                    // OBS-002: carry the writing trace across the async boundary
                    // so the processor's delivery span joins the same trace.
                    TraceParent = System.Diagnostics.Activity.Current?.Id,
                });
            }

            entry.Entity.ClearDomainEvents();
        }

        if (outboxRows.Count > 0)
        {
            context.Set<OutboxEvent>().AddRange(outboxRows);
        }
    }
}
