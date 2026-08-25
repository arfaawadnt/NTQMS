using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.DocumentControl;
using NT.QAMS.Domain.DocumentControl;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.DocumentControl;

/// <summary>
/// The Read-and-Understand compliance view (HQMS M01): the audience is resolved from the
/// target departments, acknowledgements of the current published version are matched to it,
/// and the outstanding readers fall out — the "who has not yet read" list surveyors ask for.
/// </summary>
public class DocumentComplianceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid DeptA = Guid.CreateVersion7();
    private static readonly Guid DeptB = Guid.CreateVersion7();

    private static AppDbContext NewContext(CurrentTenant tenant)
    {
        var clock = new FixedClock(Now);
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"doc-compliance-{Guid.NewGuid()}")
                .AddInterceptors(
                    new AuditStampInterceptor(clock, new FakeCurrentUser()),
                    new TenantStampInterceptor(tenant))
                .Options,
            tenant);
    }

    private static UserAccount ActiveUser(string email, params Guid[] departments)
    {
        var u = UserAccount.Create(TenantId, email, email, "hash", UserRole.Analyst);
        if (departments.Length > 0) { u.SetScope([], departments); }
        return u;
    }

    [Fact]
    public async Task Compliance_resolves_by_department_audience_and_outstanding_readers()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var db = NewContext(tenant);

        // Audience: two staff in department A. One elsewhere (dept B) and the three
        // authors/approvers (unrestricted) are outside a by-department audience.
        var a1 = ActiveUser("a1@lab.test", DeptA);
        var a2 = ActiveUser("a2@lab.test", DeptA);
        var b1 = ActiveUser("b1@lab.test", DeptB);
        var author = ActiveUser("author@lab.test");
        var reviewer = ActiveUser("reviewer@lab.test");
        var approver = ActiveUser("approver@lab.test");
        db.Users.AddRange(a1, a2, b1, author, reviewer, approver);

        // A published document, made mandatory for department A.
        var doc = ControlledDocument.Create("SOP-CAL-1", "Calibration SOP", "SOP", Guid.CreateVersion7(), "Initial", author.Id);
        doc.TenantId = TenantId;
        doc.SubmitForReview();
        doc.Recommend(reviewer.Id, Now);
        doc.Publish(approver.Id, Now);
        doc.SetReadAndUnderstand(true, DocumentAudienceScope.ByDepartment, [DeptA]);
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        // Only a1 has acknowledged the current published version.
        var ack = DocumentAcknowledgement.Record(doc.Id, doc.Code, "1.0", a1.Id, Now);
        ack.TenantId = TenantId;
        db.DocumentAcknowledgements.Add(ack);
        await db.SaveChangesAsync();

        var result = await new GetDocumentComplianceHandler(db, tenant)
            .Handle(new GetDocumentComplianceQuery(doc.Id), CancellationToken.None);

        result.AudienceCount.Should().Be(2, "only the two department-A staff are in the audience");
        result.AcknowledgedCount.Should().Be(1);
        result.OutstandingCount.Should().Be(1);
        result.CompliancePercent.Should().Be(50m);
        result.Readers.Single(r => r.UserId == a2.Id).Acknowledged.Should().BeFalse();
        result.Readers.Single(r => r.UserId == a1.Id).Acknowledged.Should().BeTrue();
    }
}
