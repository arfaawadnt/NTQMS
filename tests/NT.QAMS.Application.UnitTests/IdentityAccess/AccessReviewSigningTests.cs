using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.IdentityAccess.Commands;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Compliance;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Security;
using NT.QAMS.Infrastructure.Services;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.IdentityAccess;

/// <summary>
/// RISK-03 periodic-review batch: completing a periodic user-access review is a Part 11
/// signing ceremony, representative of the two recertification completions (this one and
/// the audit-trail review, which shares the identical no-SoD pre-validate → sign → complete
/// shape). Drives the REAL <see cref="ESignatureService"/> through
/// <see cref="CompleteAccessReviewHandler"/>, proving a valid credential mints exactly one
/// signature bound to <c>UAR:{id}</c> and that a refused completion (bad PIN) leaves none and
/// the review open.
/// </summary>
public class AccessReviewSigningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private const string Password = "Sign-Pass-123";
    private const string Pin = "2468";

    private sealed record Harness(AppDbContext Db, CompleteAccessReviewHandler Handler);

    private static Harness NewHarness()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"uar-sign-{Guid.NewGuid()}")
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
            new CompleteAccessReviewHandler(db, tenant, currentUser, new FixedClock(Now), signatures));
    }

    private static async Task<UserAccessReview> OpenReviewAsync(AppDbContext db)
    {
        var review = UserAccessReview.Open("UAR-2026-0001", DateOnly.FromDateTime(Now.UtcDateTime));
        db.UserAccessReviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    [Fact]
    public async Task Valid_signature_completes_the_review_and_records_one_manifest_entry()
    {
        var h = NewHarness();
        var review = await OpenReviewAsync(h.Db);

        await h.Handler.Handle(
            new CompleteAccessReviewCommand(review.Id, false, "All active accounts recertified; no changes.", Password, Pin),
            CancellationToken.None);

        review.Status.Should().Be(UserAccessReviewStatus.Completed);
        var signatures = await h.Db.Set<SignatureRecord>()
            .Where(s => s.SubjectRef == $"UAR:{review.Id:N}").ToListAsync();
        signatures.Should().ContainSingle();
        signatures[0].Meaning.Should().Be($"Completed access review {review.ReviewRef}: no changes");
        signatures[0].ContentHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_mints_no_signature_leaving_the_review_open()
    {
        var h = NewHarness();
        var review = await OpenReviewAsync(h.Db);

        var act = () => h.Handler.Handle(
            new CompleteAccessReviewCommand(review.Id, false, "All active accounts recertified; no changes.", Password, "0000"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SIG-001");
        review.Status.Should().Be(UserAccessReviewStatus.Open);
        (await h.Db.Set<SignatureRecord>().CountAsync())
            .Should().Be(0, "the signature is minted only after the credentials verify");
    }
}
