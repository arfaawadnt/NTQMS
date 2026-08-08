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
/// Re-opening a closed nonconformance is a 21 CFR Part 11 signing ceremony with a
/// mandatory documented reason. These tests drive the REAL
/// <see cref="ESignatureService"/> through <see cref="ReopenNcHandler"/>, proving a
/// signature carrying the reason is minted for a valid re-open and — critically for
/// an append-only ledger — that no signature is left behind when the ceremony is
/// refused on credentials or state.
/// </summary>
public class ReopenNcSigningTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Password = "Sign-Pass-123";
    private const string Pin = "2468";

    private sealed record Harness(AppDbContext Db, ReopenNcHandler Handler, UserAccount Signer);

    private static Harness NewHarness()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reopen-sign-{Guid.NewGuid()}")
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

        return new Harness(db, new ReopenNcHandler(db, currentUser, signatures), signer);
    }

    /// <summary>Builds a closed nonconformance (verified and closed by parties other than the signer).</summary>
    private static async Task<Nonconformance> ClosedNc(AppDbContext db)
    {
        var raiser = Guid.CreateVersion7();
        var closer = Guid.CreateVersion7();
        var nc = Nonconformance.Raise("NC-2026-0001", "Balance temp deviation", "Out of range", 4, 3,
            NcSourceType.Internal, raiser);
        nc.Submit();
        nc.Triage(Guid.CreateVersion7());
        nc.RecordRca(RcaMethod.FiveWhys, "Root cause: worn seal", Guid.CreateVersion7());
        var actionId = nc.PlanCapaAction(CapaActionType.Corrective, "Replace seal", Guid.CreateVersion7(), new DateOnly(2026, 8, 15));
        nc.CompleteCapaAction(actionId, Now);
        nc.SubmitForVerification();
        nc.Verify(passed: true, actorId: closer);
        nc.ConfirmEffectiveness(effective: true, actorId: closer);
        db.Nonconformances.Add(nc);
        await db.SaveChangesAsync();
        return nc;
    }

    [Fact]
    public async Task Valid_signature_reopens_the_nc_and_records_the_reason_in_the_manifest()
    {
        var h = NewHarness();
        var nc = await ClosedNc(h.Db);

        await h.Handler.Handle(
            new ReopenNcCommand(nc.Id, "Recurrence observed", Password, Pin), CancellationToken.None);

        nc.Status.Should().Be(NcStatus.ActionPlan, "a re-opened NC returns to the action-plan stage");
        nc.ReopenReason.Should().Be("Recurrence observed");

        var signatures = await h.Db.Set<SignatureRecord>()
            .Where(s => s.SubjectRef == $"NC:{nc.Id:N}").ToListAsync();
        signatures.Should().ContainSingle("exactly one Part 11 signature is minted per re-open");
        signatures[0].SignerDisplay.Should().Be("QM");
        signatures[0].Meaning.Should().Contain("Recurrence observed", "the reason is bound to the signature meaning");
        signatures[0].ContentHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_nc_closed()
    {
        var h = NewHarness();
        var nc = await ClosedNc(h.Db);

        var act = () => h.Handler.Handle(
            new ReopenNcCommand(nc.Id, "Recurrence observed", Password, "0000"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SIG-001");
        nc.Status.Should().Be(NcStatus.Closed, "a failed signing must not re-open the NC");
        (await h.Db.Set<SignatureRecord>().CountAsync()).Should().Be(0, "no signature exists for a failed ceremony");
    }

    [Fact]
    public async Task A_reopen_in_the_wrong_state_mints_no_signature()
    {
        var h = NewHarness();
        // Only submitted (Raised), never closed.
        var nc = Nonconformance.Raise("NC-2026-0002", "T", "D", 2, 2, NcSourceType.Internal, Guid.CreateVersion7());
        nc.Submit();
        h.Db.Nonconformances.Add(nc);
        await h.Db.SaveChangesAsync();

        var act = () => h.Handler.Handle(
            new ReopenNcCommand(nc.Id, "Too early", Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidStateTransitionException>()).Which.Code.Should().Be("NC-023");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the state gate is checked before the signature is minted");
    }
}
