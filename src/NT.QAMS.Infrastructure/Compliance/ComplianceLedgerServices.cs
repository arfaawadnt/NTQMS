using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Infrastructure.Compliance;

public static class LedgerHash
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Compute(string prevHash, long sequence, Guid eventId, string eventType, string payload, DateTimeOffset occurredAt)
    {
        var canonical = $"{prevHash}|{sequence}|{eventId}|{eventType}|{payload}|{occurredAt.UtcDateTime:O}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>
/// Appends the tamper-evident audit trail. Called by the OutboxProcessor for
/// every processed domain event, in the same SaveChanges as marking the row
/// processed. Chains per tenant (control-plane events use Guid.Empty). The
/// processor is single-threaded so per-tenant sequencing has no write contention.
/// </summary>
public sealed class AuditTrailAppender(AppDbContext db)
{
    private readonly Dictionary<Guid, (long Seq, string Hash)> _tips = [];

    public async Task AppendAsync(
        Guid? tenantId, Guid eventId, string eventType, string payload, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var chainTenant = tenantId ?? Guid.Empty;

        if (!_tips.TryGetValue(chainTenant, out var tip))
        {
            var last = await db.Set<AuditTrailEntry>()
                .Where(e => e.TenantId == chainTenant)
                .OrderByDescending(e => e.Sequence)
                .Select(e => new { e.Sequence, e.EntryHash })
                .FirstOrDefaultAsync(ct);
            tip = last is null ? (0L, LedgerHash.Genesis) : (last.Sequence, last.EntryHash);
        }

        var sequence = tip.Seq + 1;
        var entryHash = LedgerHash.Compute(tip.Hash, sequence, eventId, eventType, payload, occurredAt);

        db.Set<AuditTrailEntry>().Add(new AuditTrailEntry
        {
            Id = Guid.CreateVersion7(),
            TenantId = chainTenant,
            Sequence = sequence,
            EventId = eventId,
            EventType = eventType,
            Payload = payload,
            OccurredAtUtc = occurredAt,
            PrevHash = tip.Hash,
            EntryHash = entryHash,
        });

        _tips[chainTenant] = (sequence, entryHash);
    }
}

public sealed class SecurityEventLog(AppDbContext db, IClock clock) : ISecurityEventLog
{
    public async Task WriteAsync(
        string eventType, Guid? tenantId, string? actor, string? detail, CancellationToken ct)
    {
        db.Set<SecurityEvent>().Add(new SecurityEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            EventType = eventType,
            Actor = actor,
            Detail = detail,
            OccurredAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class ESignatureService(
    AppDbContext db, ICurrentTenant tenant, IPasswordHasher hasher, IClock clock)
    : IESignatureService
{
    public async Task<SignatureRecord> SignAsync(
        Guid signerId, string pin, string meaning, string subjectRef, string contentHash, CancellationToken ct)
    {
        var signer = await db.Users.SingleOrDefaultAsync(u => u.Id == signerId, ct)
            ?? throw new DomainException("SIG-404", "Signer not found.");

        if (string.IsNullOrWhiteSpace(signer.PinHash) || !hasher.Verify(signer.PinHash, pin))
        {
            throw new DomainException("SIG-001", "Electronic-signature PIN is not set or is incorrect.");
        }

        var record = new SignatureRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId ?? Guid.Empty,
            SignerId = signerId,
            SignerDisplay = signer.DisplayName,
            Meaning = meaning,
            SubjectRef = subjectRef,
            ContentHash = contentHash,
            SignedAtUtc = clock.UtcNow,
        };
        db.Set<SignatureRecord>().Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }
}

public sealed class ComplianceLedgerStore(AppDbContext db, ICurrentTenant tenant) : IComplianceLedgerStore
{
    public async Task<IReadOnlyList<AuditTrailEntry>> GetTrailAsync(string? subjectContains, int take, CancellationToken ct)
    {
        var query = db.Set<AuditTrailEntry>().AsNoTracking();
        // Defence-in-depth: filter by resolved tenant in-app (RLS also scopes at the DB).
        if (tenant.TenantId is { } tid)
        {
            query = query.Where(e => e.TenantId == tid);
        }

        if (!string.IsNullOrWhiteSpace(subjectContains))
        {
            query = query.Where(e => e.Payload.Contains(subjectContains) || e.EventType.Contains(subjectContains));
        }

        return await query.OrderByDescending(e => e.OccurredAtUtc).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SignatureRecord>> GetSignaturesAsync(int take, CancellationToken ct)
    {
        var query = db.Set<SignatureRecord>().AsNoTracking();
        if (tenant.TenantId is { } tid)
        {
            query = query.Where(s => s.TenantId == tid);
        }

        return await query.OrderByDescending(s => s.SignedAtUtc).Take(take).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SecurityEvent>> GetSecurityEventsAsync(int take, CancellationToken ct) =>
        await db.Set<SecurityEvent>().AsNoTracking()
            .OrderByDescending(s => s.OccurredAtUtc).Take(take).ToListAsync(ct);

    /// <summary>Recomputes the chain for a tenant and reports the first break, if any.</summary>
    public async Task<(bool Ok, long Verified, long? BrokenAtSequence)> VerifyChainAsync(Guid tenantId, CancellationToken ct)
    {
        var entries = await db.Set<AuditTrailEntry>().AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        var prev = LedgerHash.Genesis;
        long verified = 0;
        foreach (var e in entries)
        {
            var expected = LedgerHash.Compute(prev, e.Sequence, e.EventId, e.EventType, e.Payload, e.OccurredAtUtc);
            if (e.PrevHash != prev || e.EntryHash != expected)
            {
                return (false, verified, e.Sequence);
            }

            prev = e.EntryHash;
            verified++;
        }

        return (true, verified, null);
    }
}
