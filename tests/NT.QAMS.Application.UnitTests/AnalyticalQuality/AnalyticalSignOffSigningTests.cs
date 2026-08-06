using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.AnalyticalQuality;
using NT.QAMS.Domain.AnalyticalQuality;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Compliance;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Security;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.AnalyticalQuality;

/// <summary>
/// RISK-03 AQ batch: the 14 analytical sign-off/approve gates are Part 11 signing
/// ceremonies. These drive the REAL <see cref="ESignatureService"/> through two
/// representative handlers — a Sigma assessment (ready state = Draft, the mint path)
/// and a linearity study (wrong-state guard) — proving a valid credential mints
/// exactly one signature and that no signature is ever left behind when the sign-off
/// is refused on credentials, SoD, or state. The other 12 handlers follow the same
/// SignOff(actor, now) shape.
/// </summary>
public class AnalyticalSignOffSigningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Password = "Sign-Pass-123";
    private const string Pin = "2468";

    private sealed record Harness(
        AppDbContext Db, SigmaAssessmentWorkflowHandlers Sigma, LinearityWorkflowHandlers Linearity, Guid Signer);

    private static Harness NewHarness()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"aq-sign-{Guid.NewGuid()}")
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
            new SigmaAssessmentWorkflowHandlers(db, currentUser, new FixedClock(Now), signatures),
            new LinearityWorkflowHandlers(db, currentUser, new FixedClock(Now), signatures),
            signer.Id);
    }

    private static async Task<SigmaAssessment> DraftSigmaAsync(AppDbContext db)
    {
        var sigma = SigmaAssessment.Create("SGA-2026-0001", "Glucose", "mg/dL", 10m, 1m, 2m);
        db.SigmaAssessments.Add(sigma);
        await db.SaveChangesAsync();
        return sigma;
    }

    [Fact]
    public async Task Valid_signature_signs_off_the_assessment_and_records_one_manifest_entry()
    {
        var h = NewHarness();
        var sigma = await DraftSigmaAsync(h.Db);

        await h.Sigma.Handle(new SignOffSigmaAssessmentCommand(sigma.Id, Password, Pin), CancellationToken.None);

        sigma.State.Should().Be(SigmaAssessmentState.SignedOff);
        var signatures = await h.Db.Set<SignatureRecord>()
            .Where(s => s.SubjectRef == $"SGA:{sigma.Id:N}").ToListAsync();
        signatures.Should().ContainSingle();
        signatures[0].Meaning.Should().Be("Signed off sigma assessment");
        signatures[0].ContentHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_assessment_draft()
    {
        var h = NewHarness();
        var sigma = await DraftSigmaAsync(h.Db);

        var act = () => h.Sigma.Handle(
            new SignOffSigmaAssessmentCommand(sigma.Id, Password, "0000"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SIG-001");
        sigma.State.Should().Be(SigmaAssessmentState.Draft);
        (await h.Db.Set<SignatureRecord>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_preparer_cannot_sign_off_their_own_study_and_no_signature_is_minted()
    {
        var h = NewHarness();
        var sigma = await DraftSigmaAsync(h.Db);
        sigma.CreatedByUserId = h.Signer; // the signer is the preparer
        await h.Db.SaveChangesAsync();

        var act = () => h.Sigma.Handle(
            new SignOffSigmaAssessmentCommand(sigma.Id, Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SOD-AQ-001");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the SoD gate is checked before the signature is minted");
    }

    [Fact]
    public async Task A_sign_off_in_the_wrong_state_mints_no_signature()
    {
        var h = NewHarness();
        // A freshly configured linearity study is not yet Calculated.
        var study = LinearityStudy.Configure("LIN-2026-0001", "Sodium", "mmol/L", "ISE", 10m);
        h.Db.LinearityStudies.Add(study);
        await h.Db.SaveChangesAsync();

        var act = () => h.Linearity.Handle(
            new SignOffLinearityCommand(study.Id, Password, Pin), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidStateTransitionException>()).Which.Code.Should().Be("LIN-012");
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the state gate is checked before the signature is minted");
    }
}
