using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Improvement.Commands;
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
/// RISK-03 pilot: verifying corrective-action effectiveness is a 21 CFR Part 11
/// signing ceremony. These tests drive the REAL <see cref="ESignatureService"/>
/// (real password hasher, real signature persistence) through
/// <see cref="VerifyNcHandler"/>, proving that a signature is minted for a valid
/// verification and — critically for an append-only ledger — that no signature is
/// ever left behind when a verification is refused on credentials, SoD, or state.
/// </summary>
public class VerifyNcSigningTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Password = "Sign-Pass-123";
    private const string Pin = "2468";

    private sealed record Harness(
        AppDbContext Db, VerifyNcHandler Handler, UserAccount Signer, FakeCurrentUser User);

    private static Harness NewHarness()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"verify-sign-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(new FixedClock(Now), new FakeCurrentUser()),
                new TenantStampInterceptor(tenant))
            .Options;
        var db = new AppDbContext(options, tenant);

        var hasher = new IdentityPasswordHasher();
        var signer = UserAccount.Create(
            TenantId, "qm@lab.test", "QM", hasher.Hash(Password), UserRole.QualityManager);
        signer.SetPin(hasher.Hash(Pin));
        db.Users.Add(signer);
        db.SaveChanges();

        var currentUser = new FakeCurrentUser { UserId = signer.Id, DisplayName = "QM" };
        var signatures = new ESignatureService(
            db, tenant, hasher, new FixedClock(Now), new SecurityEventLog(db, new FixedClock(Now)));

        return new Harness(db, new VerifyNcHandler(db, currentUser, signatures), signer, currentUser);
    }

    /// <summary>Builds a nonconformance sitting in PendingVerification, raised by <paramref name="raisedBy"/>.</summary>
    private static async Task<Nonconformance> PendingVerificationNc(AppDbContext db, Guid raisedBy)
    {
        var nc = Nonconformance.Raise("NC-2026-0001", "Balance temp deviation", "Out of range", 4, 3,
            NcSourceType.Internal, raisedBy);
        nc.Submit();
        nc.Triage(Guid.CreateVersion7());
        nc.RecordRca(RcaMethod.FiveWhys, "Root cause: worn seal", Guid.CreateVersion7());
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Replace seal", Guid.CreateVersion7(), new DateOnly(2026, 8, 15));
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync();
        return nc;
    }

    [Fact]
    public async Task Valid_signature_advances_the_nc_and_records_exactly_one_manifest_entry()
    {
        var h = NewHarness();
        var nc = await PendingVerificationNc(h.Db, raisedBy: Guid.CreateVersion7());

        await h.Handler.Handle(new VerifyNcCommand(nc.Id, Passed: true, Password, Pin), CancellationToken.None);

        nc.Status.Should().Be(NcStatus.EffectivenessCheck, "a passed verification advances the NC");

        var signatures = await h.Db.Set<SignatureRecord>()
            .Where(s => s.SubjectRef == $"NC:{nc.Id:N}").ToListAsync();
        signatures.Should().ContainSingle("exactly one Part 11 signature is minted per verification");
        signatures[0].SignerDisplay.Should().Be("QM");
        signatures[0].Meaning.Should().Contain("passed");
        signatures[0].ContentHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_nc_pending()
    {
        var h = NewHarness();
        var nc = await PendingVerificationNc(h.Db, raisedBy: Guid.CreateVersion7());

        var act = () => h.Handler.Handle(
            new VerifyNcCommand(nc.Id, Passed: true, Password, "0000"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SIG-001");
        nc.Status.Should().Be(NcStatus.PendingVerification, "a failed signing must not advance the NC");
        (await h.Db.Set<SignatureRecord>().CountAsync()).Should().Be(0, "no signature exists for a failed ceremony");
    }

    [Fact]
    public async Task The_raiser_cannot_sign_their_own_verification_and_no_signature_is_minted()
    {
        var h = NewHarness();
        // SoD: the person who signs (the current user / signer) is also the raiser.
        var nc = await PendingVerificationNc(h.Db, raisedBy: h.Signer.Id);

        var act = () => h.Handler.Handle(
            new VerifyNcCommand(nc.Id, Passed: true, Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SOD-CAPA-002");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the SoD gate is checked before the signature is minted");
    }

    [Fact]
    public async Task A_verification_in_the_wrong_state_mints_no_signature()
    {
        var h = NewHarness();
        // Only submitted (Raised), not yet PendingVerification.
        var nc = Nonconformance.Raise("NC-2026-0002", "T", "D", 2, 2, NcSourceType.Internal, Guid.CreateVersion7());
        nc.Submit();
        h.Db.Nonconformances.Add(nc);
        await h.Db.SaveChangesAsync();

        var act = () => h.Handler.Handle(
            new VerifyNcCommand(nc.Id, Passed: true, Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidStateTransitionException>()).Which.Code.Should().Be("NC-021");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the state gate is checked before the signature is minted");
    }
}
