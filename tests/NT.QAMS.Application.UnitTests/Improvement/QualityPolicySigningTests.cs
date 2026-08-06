using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Improvement;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Compliance;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Security;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Improvement;

/// <summary>
/// RISK-03 non-AQ batch: approving the quality policy is a Part 11 signing ceremony,
/// representative of the approve/sign-off gates (audit sign-off, change approve, NC
/// close). Drives the REAL <see cref="ESignatureService"/> through
/// <see cref="QualityPolicyWorkflowHandlers"/>, proving a valid credential mints exactly
/// one signature and that a refused approval (bad PIN, or SoD) leaves none.
/// </summary>
public class QualityPolicySigningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Effective = new(2026, 9, 1);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Password = "Sign-Pass-123";
    private const string Pin = "2468";

    private sealed record Harness(AppDbContext Db, QualityPolicyWorkflowHandlers Handler, Guid Signer);

    private static Harness NewHarness()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"qp-sign-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(new FixedClock(Now), new FakeCurrentUser()),
                new TenantStampInterceptor(tenant))
            .Options;
        var db = new AppDbContext(options, tenant);

        var hasher = new IdentityPasswordHasher();
        var signer = UserAccount.Create(TenantId, "qm@lab.test", "QM", hasher.Hash(Password), UserRole.QualityManager);
        signer.SetPin(hasher.Hash(Pin));
        db.Users.Add(signer);
        db.SaveChanges();

        var currentUser = new FakeCurrentUser { UserId = signer.Id, DisplayName = "QM" };
        var signatures = new ESignatureService(
            db, tenant, hasher, new FixedClock(Now), new SecurityEventLog(db, new FixedClock(Now)));

        return new Harness(
            db,
            new QualityPolicyWorkflowHandlers(db, currentUser, new FixedClock(Now), signatures),
            signer.Id);
    }

    private static async Task<QualityPolicy> DraftPolicyAsync(AppDbContext db, Guid? author = null)
    {
        var policy = QualityPolicy.Draft("QP-2026-0001", 1, "We are committed to accurate, impartial testing.");
        db.QualityPolicies.Add(policy);
        await db.SaveChangesAsync();
        // Stamp the author AFTER insert: the AuditStampInterceptor sets CreatedByUserId only on Add,
        // so setting it on a subsequent Modified save sticks (it stamps UpdatedBy, not CreatedBy).
        if (author is { } a)
        {
            policy.CreatedByUserId = a;
            await db.SaveChangesAsync();
        }
        return policy;
    }

    [Fact]
    public async Task Valid_signature_activates_the_policy_and_records_one_manifest_entry()
    {
        var h = NewHarness();
        var policy = await DraftPolicyAsync(h.Db); // no author stamped → SoD no-op

        await h.Handler.Handle(
            new ApproveQualityPolicyCommand(policy.Id, Effective, Password, Pin), CancellationToken.None);

        policy.Status.Should().Be(QualityPolicyStatus.Active);
        var signatures = await h.Db.Set<SignatureRecord>()
            .Where(s => s.SubjectRef == $"QP:{policy.Id:N}").ToListAsync();
        signatures.Should().ContainSingle();
        signatures[0].Meaning.Should().Be("Approved the quality policy");
        signatures[0].ContentHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_policy_draft()
    {
        var h = NewHarness();
        var policy = await DraftPolicyAsync(h.Db);

        var act = () => h.Handler.Handle(
            new ApproveQualityPolicyCommand(policy.Id, Effective, Password, "0000"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SIG-001");
        policy.Status.Should().Be(QualityPolicyStatus.Draft);
        (await h.Db.Set<SignatureRecord>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_author_cannot_approve_their_own_policy_and_no_signature_is_minted()
    {
        var h = NewHarness();
        var policy = await DraftPolicyAsync(h.Db, author: h.Signer); // the signer authored it

        var act = () => h.Handler.Handle(
            new ApproveQualityPolicyCommand(policy.Id, Effective, Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SOD-QP-001");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the SoD gate is checked before the signature is minted");
    }
}
