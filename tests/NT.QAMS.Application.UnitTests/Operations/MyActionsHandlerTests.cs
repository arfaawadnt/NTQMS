using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Sla;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Domain.RiskGovernance;
using NT.QAMS.Domain.Sla;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.Infrastructure.Services;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Operations;

/// <summary>
/// The unified "My Tasks" action centre (<see cref="GetMyActionsHandler"/>): the
/// live read model unions pending actions across sources for the signed-in user.
/// Runs on EF InMemory (the query must translate on both InMemory and PostgreSQL).
/// </summary>
public sealed class MyActionsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private sealed class FakePrivileges : IUserPrivileges
    {
        public bool IsResolved => true;
        public bool IsPlatformAdmin { get; init; }
        public Guid? RoleId => null;
        public string? RoleName { get; init; }
        public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();
        public IReadOnlySet<Guid> AllowedBranchIds => new HashSet<Guid>();
        public IReadOnlySet<Guid> AllowedDepartmentIds => new HashSet<Guid>();
        public bool HasBranchRestriction => false;
        public bool HasDepartmentRestriction => false;
        public string? PreferredLanguage => null;
        public bool Has(string permissionKey) => Permissions.Contains(permissionKey);
        public bool CanAccessBranch(Guid? branchId) => true;
        public bool CanAccessDepartment(Guid? departmentId) => true;
    }

    private static AppDbContext NewDb()
    {
        var tenant = new CurrentTenant();
        tenant.Set(TenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"my-actions-{Guid.NewGuid()}")
            .AddInterceptors(
                new AuditStampInterceptor(new FixedClock(Now), new FakeCurrentUser()),
                new TenantStampInterceptor(tenant))
            .Options;
        return new AppDbContext(options, tenant);
    }

    [Fact]
    public async Task Feed_unions_pending_actions_across_sources_for_the_user()
    {
        var db = NewDb();
        var me = UserAccount.Create(TenantId, "me@lab.test", "Me", "hash", UserRole.QualityManager);
        db.Users.Add(me);

        // Manual task assigned to me.
        db.WorkTasks.Add(WorkTask.Create("Review the SOP", "DOC-1", me.Id, null, new DateOnly(2026, 8, 1)));

        // NC assigned to me (investigation).
        var assigned = Nonconformance.Raise("NC-1", "Assigned to me", "d", 4, 3, NcSourceType.Internal, Guid.CreateVersion7());
        assigned.Submit();
        assigned.Triage(me.Id);
        db.Nonconformances.Add(assigned);

        // NC with a CAPA action I own.
        var capaNc = Nonconformance.Raise("NC-2", "Has my CAPA", "d", 2, 2, NcSourceType.Internal, Guid.CreateVersion7());
        capaNc.Submit();
        capaNc.Triage(Guid.CreateVersion7());
        capaNc.RecordRca(RcaMethod.FiveWhys, "root", Guid.CreateVersion7());
        capaNc.PlanCapaAction(CapaActionType.Corrective, "Fix the seal", me.Id, new DateOnly(2026, 8, 10));
        db.Nonconformances.Add(capaNc);

        // NC awaiting verification (I may sign, and I did not raise it).
        var verifyNc = Nonconformance.Raise("NC-3", "Awaiting verify", "d", 5, 4, NcSourceType.Internal, Guid.CreateVersion7());
        verifyNc.Submit();
        verifyNc.Triage(Guid.CreateVersion7());
        verifyNc.RecordRca(RcaMethod.FiveWhys, "root", Guid.CreateVersion7());
        var actionId = verifyNc.PlanCapaAction(CapaActionType.Corrective, "Fix", Guid.CreateVersion7(), new DateOnly(2026, 8, 10));
        verifyNc.CompleteCapaAction(actionId, Now);
        verifyNc.SubmitForVerification();
        db.Nonconformances.Add(verifyNc);

        // Risk with a mitigation action I own.
        var risk = RiskItem.Assess("RSK-1", "My risk", "Operational", 4, 4);
        risk.AddMitigationAction("Mitigate it", me.Id, new DateOnly(2026, 8, 5));
        db.Risks.Add(risk);

        // Quality objective I own.
        var objective = QualityObjective.Define(
            "OBJ-1", "Reduce TAT", "d", "TAT", "days", 5m, ObjectiveDirection.AtMost, me.Id,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        db.QualityObjectives.Add(objective);

        // Management review I participate in.
        var review = ManagementReview.Schedule("MRV-1", "Q3 review", new DateOnly(2026, 9, 1), "chair", participantUserIds: [me.Id]);
        db.ManagementReviews.Add(review);

        await db.SaveChangesAsync();

        var handler = new GetMyActionsHandler(
            db, new FakeCurrentUser { UserId = me.Id },
            new FakePrivileges { RoleName = "Quality Manager", Permissions = new HashSet<string> { "nc.sign" } },
            new FixedClock(Now));

        var feed = await handler.Handle(new GetMyActionsQuery(), CancellationToken.None);

        feed.Select(i => i.Category).Should().Contain(["task", "nc", "capa", "risk", "objective", "review"]);
    }
}
