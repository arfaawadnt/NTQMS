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
