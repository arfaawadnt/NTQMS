using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.RiskGovernance;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.RiskGovernance;

/// <summary>
/// Audit finding M-01: the Part 11 ceremony must refuse a segregation-of-duties
/// violation BEFORE the signature is minted. <c>ESignatureService.SignAsync</c>
/// persists through its own SaveChanges, so a signature minted ahead of the
/// aggregate's SoD guard survives the refusal — a durable record whose signed
/// meaning ("Approved/Ratified change X") is false. These tests pin the order:
/// SoD refusal ⇒ zero signature calls.
/// </summary>
public class ChangeCeremonySodOrderingTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid Proposer = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Counts ceremony invocations; the assertions care about order, not content.</summary>
    private sealed class CountingSignatureService : IESignatureService
    {
        public int Calls { get; private set; }

        public Task<SignatureRecord> SignAsync(
            Guid signerId, string password, string pin, string meaning, string subjectRef, string contentHash,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new SignatureRecord
            {
                Id = Guid.CreateVersion7(),
                SignerId = signerId,
                SignerDisplay = "fake",
                Meaning = meaning,
                SubjectRef = subjectRef,
                ContentHash = contentHash,
                SignedAtUtc = Now,
            });
        }
    }

    private static AppDbContext NewContext(FakeCurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"chg-sod-{Guid.NewGuid()}")
            .AddInterceptors(new TenantStampInterceptor(tenant))
            .Options, tenant);

    private static async Task<(AppDbContext Db, ChangeRequest Change)> SeedAsync(Func<ChangeRequest> factory)
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        var db = NewContext(tenant);
        var change = factory();
        change.LinkRiskAssessment(Guid.CreateVersion7());
        change.TenantId = TenantId;
        db.ChangeRequests.Add(change);
        await db.SaveChangesAsync();
        return (db, change);
    }

    [Fact]
    public async Task Ratification_by_the_proposer_is_refused_before_any_signature_is_minted()
    {
        var (db, change) = await SeedAsync(() => ChangeRequest.ProposeEmergency(
            "CHG-2026-0001", "Emergency firmware patch", "Bypass on analyser", Proposer, new DateOnly(2026, 9, 15)));
        var signatures = new CountingSignatureService();
        var handler = new RatifyChangeHandler(
            db, new FakeCurrentUser { UserId = Proposer }, new FixedClock(Now), signatures);

        var act = () => handler.Handle(
            new RatifyChangeCommand(change.Id, "done", "pw", "1234"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CHG-032");
        signatures.Calls.Should().Be(0,
            "a refused ratification must leave no signature in the append-only ledger");
    }

    [Fact]
    public async Task High_impact_self_approval_is_refused_before_any_signature_is_minted()
    {
        var (db, change) = await SeedAsync(() => ChangeRequest.Propose(
            "CHG-2026-0002", "LIS interface swap", "High-impact routing change", Proposer, ChangeImpactLevel.High));
        var signatures = new CountingSignatureService();
        var handler = new ApproveChangeHandler(
            db, new FakeCurrentUser { UserId = Proposer }, new FixedClock(Now), signatures);

        var act = () => handler.Handle(
            new ApproveChangeCommand(change.Id, "pw", "1234"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CHG-016");
        signatures.Calls.Should().Be(0,
            "a refused approval must leave no signature in the append-only ledger");
    }

    [Fact]
    public async Task An_independent_ratifier_still_signs_exactly_once_and_closes_the_change()
    {
        var (db, change) = await SeedAsync(() => ChangeRequest.ProposeEmergency(
            "CHG-2026-0003", "Emergency firmware patch", "Bypass on analyser", Proposer, new DateOnly(2026, 9, 15)));
        var signatures = new CountingSignatureService();
        var handler = new RatifyChangeHandler(
            db, new FakeCurrentUser { UserId = Guid.CreateVersion7() }, new FixedClock(Now), signatures);

        await handler.Handle(new RatifyChangeCommand(change.Id, "done", "pw", "1234"), CancellationToken.None);

        signatures.Calls.Should().Be(1);
        (await db.ChangeRequests.SingleAsync()).Status.Should().Be(ChangeStatus.Closed);
    }
}
