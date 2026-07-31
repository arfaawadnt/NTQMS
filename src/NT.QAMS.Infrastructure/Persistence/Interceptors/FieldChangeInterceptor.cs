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
        TenantId = TenantOf(entry),
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

    /// <summary>
    /// The tenant this change belongs to, so it appears in that tenant's own
    /// compliance view (the read filters on <c>tenant_id</c>).
    /// <para>
    /// Order matters. A tenant-scoped aggregate carries the value directly. An
    /// owned child does not: since the child tables gained tenancy it holds a
    /// <b>shadow</b> <c>TenantId</c>, which no CLR cast can reach — reading only
    /// the interface left 19,296 privilege-detail rows stamped NULL and therefore
    /// invisible to the tenant whose privileges changed. The request tenant is the
    /// last resort, and is legitimately absent on elevated paths (seeding,
    /// provisioning), which is exactly where the shadow value has to answer.
    /// </para>
    /// </summary>
    private Guid? TenantOf(EntityEntry entry)
    {
        if (entry.Entity is ITenantScoped { TenantId: var scoped } && scoped != Guid.Empty)
        {
            return scoped;
        }

        if (entry.Metadata.FindProperty("TenantId") is { } tenantProperty
            && tenantProperty.IsShadowProperty()
            && entry.Property("TenantId").CurrentValue is Guid shadow && shadow != Guid.Empty)
        {
            return shadow;
        }

        if (entry.Entity is IOptionallyTenantScoped { TenantId: { } optional })
        {
            return optional;
        }

        return currentTenant.TenantId;
    }

    /// <summary>
    /// The record's identity as the ledger and its readers understand it.
    /// <para>
    /// Since primary keys became tenant-first (schema hardening Phase 5), the raw
    /// key would render as <c>tenant|id</c> — a different value from every
    /// historical row and from what <c>GetFieldChangesQuery(entityId)</c> looks
    /// up. The tenant is already its own column on this ledger, so it is dropped
    /// from the rendered identity and the contract is unchanged.
    /// </para>
    /// </summary>
    private static string RenderKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return "(keyless)";
        }

        var identity = key.Properties.Where(p => p.Name != "TenantId").ToList();
        if (identity.Count == 0)
        {
            identity = [.. key.Properties];
        }

        return string.Join('|', identity.Select(p => Render(entry.Property(p.Name).CurrentValue)));
    }

    private static string? Render(object? value) => value switch
    {
        null => null,
        DateTimeOffset dto => dto.ToString("O"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        _ => value.ToString(),
    };
}
