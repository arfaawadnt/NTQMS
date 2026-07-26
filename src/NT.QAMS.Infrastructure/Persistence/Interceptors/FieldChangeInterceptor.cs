using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.Notifications;
using NT.QAMS.Domain.Reporting;
using NT.QAMS.Infrastructure.Persistence.Outbox;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.MultiTenancy;

namespace NT.QAMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Part 11 §11.10(e) field-level audit: captures old-value/new-value diffs for
/// every business-record change in the SAME transaction as the change itself
/// (contemporaneous by construction — an app crash after the write cannot lose
/// the trail). Ledger/outbox/read-model tables are excluded (they are the
/// trail, or derived data); credential-bearing properties are redacted at
/// capture so secrets never reach the ledger.
/// </summary>
public sealed class FieldChangeInterceptor(
    IClock clock, ICurrentUser currentUser, ICurrentTenant currentTenant, ICurrentChangeReason changeReason)
    : SaveChangesInterceptor
{
    /// <summary>Entity types that must never generate field rows (the ledgers themselves, plumbing, and derived data).</summary>
    private static readonly HashSet<Type> Excluded =
    [
        typeof(FieldChangeRecord), typeof(AuditTrailEntry), typeof(SignatureRecord), typeof(SecurityEvent),
        typeof(OutboxEvent), typeof(KpiSnapshot), typeof(NotificationDispatch), typeof(RefCounter),
    ];

    /// <summary>Property-name fragments whose values are redacted (never stored in clear).</summary>
    private static readonly string[] Sensitive = ["password", "secret", "pin", "hash", "token"];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var records = new List<FieldChangeRecord>();
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (Excluded.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    records.Add(Record(entry, "Created", null, null, null));
                    break;
                case EntityState.Deleted:
                    records.Add(Record(entry, "Deleted", null, null, null));
                    break;
                case EntityState.Modified:
                    records.AddRange(ModifiedRows(entry));
                    break;
            }
        }

        if (records.Count > 0)
        {
            context.Set<FieldChangeRecord>().AddRange(records);
        }
    }

    private IEnumerable<FieldChangeRecord> ModifiedRows(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
            {
                continue;
            }

            var redact = IsSensitive(property.Metadata.Name);
            yield return Record(
                entry, "Modified", property.Metadata.Name,
                redact ? "«redacted»" : Render(property.OriginalValue),
                redact ? "«redacted»" : Render(property.CurrentValue));
        }
    }

    private FieldChangeRecord Record(
        EntityEntry entry, string action, string? property, string? oldValue, string? newValue) => new()
    {
        // Owned children (and other non-ITenantScoped entities) inherit the
        // request's tenant, so a child change is attributed to — and visible in —
        // the owning tenant's audit trail, and satisfies the RLS WITH CHECK.
        TenantId = (entry.Entity as ITenantScoped)?.TenantId ?? currentTenant.TenantId,
        EntityType = entry.Entity.GetType().Name,
        EntityId = RenderKey(entry),
        Action = action,
        Property = property,
        OldValue = oldValue,
        NewValue = newValue,
        ActorId = currentUser.UserId,
        Actor = currentUser.DisplayName ?? "system",
        Reason = changeReason.Reason,
        OccurredAtUtc = clock.UtcNow,
    };

    public static bool IsSensitive(string propertyName) =>
        Sensitive.Any(s => propertyName.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static string RenderKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return "(keyless)";
        }

        return string.Join('|', key.Properties.Select(p => Render(entry.Property(p.Name).CurrentValue)));
    }

    private static string? Render(object? value) => value switch
    {
        null => null,
        DateTimeOffset dto => dto.ToString("O"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        _ => value.ToString(),
    };
}
