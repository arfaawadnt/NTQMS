using NT.QAMS.Domain.ComplianceLedger;

namespace NT.QAMS.Application.Abstractions;

/// <summary>TOTP (RFC 6238) generation and verification. Infrastructure supplies the crypto.</summary>
public interface ITotpService
{
    string GenerateSecret();
    bool Verify(string secret, string code, DateTimeOffset now);
    string BuildOtpAuthUri(string secret, string account, string issuer);
}

/// <summary>Appends security events to the compliance ledger.</summary>
public interface ISecurityEventLog
{
    Task WriteAsync(
        string eventType, Guid? tenantId, string? actor, string? detail, CancellationToken cancellationToken);
}

/// <summary>
/// Verifies BOTH e-signature components (account password + signature PIN,
/// 21 CFR Part 11 §11.200(a)(1)) and appends an immutable signature record.
/// The only path to a signature; callers pass meaning + subject + content hash.
/// </summary>
public interface IESignatureService
{
    Task<SignatureRecord> SignAsync(
        Guid signerId, string password, string pin, string meaning, string subjectRef, string contentHash,
        CancellationToken cancellationToken);
}

/// <summary>Read access to the compliance ledgers (append happens via the outbox/services).</summary>
public interface IComplianceLedgerStore
{
    Task<IReadOnlyList<AuditTrailEntry>> GetTrailAsync(string? subjectContains, int take, CancellationToken ct);
    Task<IReadOnlyList<SignatureRecord>> GetSignaturesAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<SignatureRecord>> GetSignaturesForSubjectAsync(string subjectRef, CancellationToken ct);
    Task<IReadOnlyList<SecurityEvent>> GetSecurityEventsAsync(int take, CancellationToken ct);
    Task<(bool Ok, long Verified, long? BrokenAtSequence)> VerifyChainAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Ledger volumes for a review period — evidence of coverage for the periodic audit-trail review.</summary>
    Task<(int Events, int FieldChanges)> CountForPeriodAsync(
        Guid tenantId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);
}
