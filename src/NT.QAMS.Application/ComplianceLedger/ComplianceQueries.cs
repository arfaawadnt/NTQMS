using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.ComplianceLedger;

public sealed record GetAuditTrailQuery(string? SubjectContains, int Take = 200)
    : IQuery<IReadOnlyList<AuditTrailEntry>>;

public sealed class GetAuditTrailHandler(IComplianceLedgerStore store)
    : IQueryHandler<GetAuditTrailQuery, IReadOnlyList<AuditTrailEntry>>
{
    public Task<IReadOnlyList<AuditTrailEntry>> Handle(GetAuditTrailQuery q, CancellationToken ct) =>
        store.GetTrailAsync(q.SubjectContains, Math.Clamp(q.Take, 1, 1000), ct);
}

/// <summary>
/// The audit trail for one record (its detail-page timeline): only the entries
/// that record itself produced, matched on the aggregate id. Distinct from
/// <see cref="GetAuditTrailQuery"/>, whose substring search backs the ledger-wide
/// admin search box and would surface entries that merely reference the id.
/// </summary>
public sealed record GetRecordAuditTrailQuery(Guid SubjectId, int Take = 200)
    : IQuery<IReadOnlyList<AuditTrailEntry>>;

public sealed class GetRecordAuditTrailHandler(IComplianceLedgerStore store)
    : IQueryHandler<GetRecordAuditTrailQuery, IReadOnlyList<AuditTrailEntry>>
{
    public Task<IReadOnlyList<AuditTrailEntry>> Handle(GetRecordAuditTrailQuery q, CancellationToken ct) =>
        store.GetTrailForRecordAsync(q.SubjectId, Math.Clamp(q.Take, 1, 1000), ct);
}

public sealed record GetSignatureLogQuery(int Take = 200) : IQuery<IReadOnlyList<SignatureRecord>>;

public sealed class GetSignatureLogHandler(IComplianceLedgerStore store)
    : IQueryHandler<GetSignatureLogQuery, IReadOnlyList<SignatureRecord>>
{
    public Task<IReadOnlyList<SignatureRecord>> Handle(GetSignatureLogQuery q, CancellationToken ct) =>
        store.GetSignaturesAsync(Math.Clamp(q.Take, 1, 1000), ct);
}

public sealed record GetSecurityEventsQuery(int Take = 200) : IQuery<IReadOnlyList<SecurityEvent>>;

public sealed class GetSecurityEventsHandler(IComplianceLedgerStore store)
    : IQueryHandler<GetSecurityEventsQuery, IReadOnlyList<SecurityEvent>>
{
    public Task<IReadOnlyList<SecurityEvent>> Handle(GetSecurityEventsQuery q, CancellationToken ct) =>
        store.GetSecurityEventsAsync(Math.Clamp(q.Take, 1, 1000), ct);
}

/// <summary>Nightly (or on-demand) hash-chain integrity check for the current tenant.</summary>
public sealed record VerifyChainQuery(Guid TenantId) : IQuery<ChainVerificationDto>;

public sealed record ChainVerificationDto(bool Ok, long VerifiedEntries, long? BrokenAtSequence);

public sealed class VerifyChainHandler(IComplianceLedgerStore store)
    : IQueryHandler<VerifyChainQuery, ChainVerificationDto>
{
    public async Task<ChainVerificationDto> Handle(VerifyChainQuery q, CancellationToken ct)
    {
        var (ok, verified, broken) = await store.VerifyChainAsync(q.TenantId, ct);
        return new ChainVerificationDto(ok, verified, broken);
    }
}

/// <summary>
/// Field-level change history (Part 11 §11.10(e)): optionally filtered to one
/// record's id. Tenant-scoped in-app (RLS also scopes at the database).
/// </summary>
public sealed record GetFieldChangesQuery(string? EntityId, int Take = 200)
    : IQuery<IReadOnlyList<FieldChangeRecord>>;

public sealed class GetFieldChangesHandler(IAppDbContext db, ICurrentTenant tenant)
    : IQueryHandler<GetFieldChangesQuery, IReadOnlyList<FieldChangeRecord>>
{
    public async Task<IReadOnlyList<FieldChangeRecord>> Handle(GetFieldChangesQuery q, CancellationToken ct)
    {
        var query = db.FieldChanges.AsNoTracking()
            .Where(f => f.TenantId == tenant.TenantId);
        if (!string.IsNullOrWhiteSpace(q.EntityId))
        {
            query = query.Where(f => f.EntityId == q.EntityId);
        }

        return await query
            .OrderByDescending(f => f.OccurredAtUtc)
            .Take(Math.Clamp(q.Take, 1, 1000))
            .ToListAsync(ct);
    }
}

/// <summary>
/// Signature manifest for one record (Part 11 §11.50: signature information
/// must be shown on the signed record) — readable by any user who can open
/// the record, unlike the ledger-wide queries.
/// </summary>
public sealed record GetSignaturesForSubjectQuery(string SubjectRef)
    : IQuery<IReadOnlyList<SignatureRecord>>;

public sealed class GetSignaturesForSubjectHandler(IComplianceLedgerStore store)
    : IQueryHandler<GetSignaturesForSubjectQuery, IReadOnlyList<SignatureRecord>>
{
    public Task<IReadOnlyList<SignatureRecord>> Handle(GetSignaturesForSubjectQuery q, CancellationToken ct) =>
        store.GetSignaturesForSubjectAsync(q.SubjectRef, ct);
}
