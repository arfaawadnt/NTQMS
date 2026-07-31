using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Authorization;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IdentityAccess;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.SharedKernel.Primitives;
using Xunit;

namespace NT.QAMS.Application.UnitTests.Authorization;

/// <summary>
/// The role-administration handlers, centred on the module's cross-aggregate
/// invariant: no sequence of edits may leave the tenant without an active user
/// who can still manage roles (ROLE-006 — the lockout guard).
/// </summary>
public class RoleHandlersTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static AppDbContext NewContext()
    {
        var tenant = new FakeCurrentTenant { TenantId = TenantId };
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"role-handlers-{Guid.NewGuid()}")
                .AddInterceptors(new NT.QAMS.Infrastructure.Persistence.Interceptors.TenantStampInterceptor(tenant))
                .Options,
            tenant);
    }

    private static async Task<(Role Admin, UserAccount Holder)> SeedAdminAsync(AppDbContext db)
    {
        var admin = Role.CreateSystem("Tenant Administrator", null, PermissionCatalog.AllKeys.ToArray());
        admin.TenantId = TenantId;
        db.Roles.Add(admin);

        var holder = UserAccount.Create(TenantId, "admin@lab.test", "Admin", "hash", UserRole.TenantAdmin);
        holder.AssignRole(admin.Id);
        db.Users.Add(holder);

        await db.SaveChangesAsync(CancellationToken.None);
        return (admin, holder);
    }

    [Fact]
    public async Task Creating_a_role_rejects_a_duplicate_name_case_insensitively()
    {
        await using var db = NewContext();
        var handler = new CreateRoleHandler(db);

        await handler.Handle(new CreateRoleCommand("Bench Lead", null, [], null), CancellationToken.None);
        var act = () => handler.Handle(new CreateRoleCommand("  bench LEAD ", null, [], null), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ROLE-007");
    }

    [Fact]
    public async Task Revoking_manage_roles_from_the_last_holding_role_is_refused()
    {
        await using var db = NewContext();
        var (admin, _) = await SeedAdminAsync(db);
        var handler = new SetRolePermissionsHandler(db);

        var withoutManage = PermissionCatalog.AllKeys
            .Where(k => k != PermissionCatalog.ManageRoles).ToArray();
        var act = () => handler.Handle(
            new SetRolePermissionsCommand(admin.Id, withoutManage, "Attempted lockout."), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ROLE-006");
    }

    [Fact]
    public async Task Revoking_manage_roles_is_allowed_once_another_active_user_holds_it()
    {
        await using var db = NewContext();
        var (admin, _) = await SeedAdminAsync(db);

        var second = Role.Create("Privilege Officer", null, [PermissionCatalog.ManageRoles]);
        second.TenantId = TenantId;
        db.Roles.Add(second);
        var officer = UserAccount.Create(TenantId, "officer@lab.test", "Officer", "hash", UserRole.QualityManager);
        officer.AssignRole(second.Id);
        db.Users.Add(officer);
        await db.SaveChangesAsync(CancellationToken.None);

        var withoutManage = PermissionCatalog.AllKeys
            .Where(k => k != PermissionCatalog.ManageRoles).ToArray();
        await new SetRolePermissionsHandler(db).Handle(
            new SetRolePermissionsCommand(admin.Id, withoutManage, "Handing privilege administration over."),
            CancellationToken.None);

        (await db.Roles.SingleAsync(r => r.Id == admin.Id)).Grants(PermissionCatalog.ManageRoles)
            .Should().BeFalse();
    }

    [Fact]
    public async Task Moving_the_last_manager_to_an_unprivileged_role_is_refused()
    {
        await using var db = NewContext();
        var (_, holder) = await SeedAdminAsync(db);

        var basic = Role.Create("Reader", null,
            [PermissionCatalog.Key(PermissionCatalog.Nonconformances, PermissionAction.View)]);
        basic.TenantId = TenantId;
        db.Roles.Add(basic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new NT.QAMS.Application.IdentityAccess.Commands.AssignUserRoleHandler(
            db, CurrentTenantFor(TenantId));
        var act = () => handler.Handle(
            new NT.QAMS.Application.IdentityAccess.Commands.AssignUserRoleCommand(holder.Id, basic.Id),
            CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ROLE-006");
    }

    [Fact]
    public async Task Deactivating_the_last_managing_role_is_refused()
    {
        await using var db = NewContext();
        var (admin, _) = await SeedAdminAsync(db);

        // System roles cannot be deactivated anyway; use a tenant-defined
        // manager to prove the guard fires before the domain rule.
        var custom = Role.Create("Custom Admin", null, [PermissionCatalog.ManageRoles]);
        custom.TenantId = TenantId;
        db.Roles.Add(custom);
        var holder2 = UserAccount.Create(TenantId, "c@lab.test", "C", "hash", UserRole.QualityManager);
        holder2.AssignRole(custom.Id);
        db.Users.Add(holder2);
        await db.SaveChangesAsync(CancellationToken.None);

        // Strip the seeded admin role of manage first (legal: custom still holds it)…
        await new SetRolePermissionsHandler(db).Handle(
            new SetRolePermissionsCommand(
                admin.Id,
                PermissionCatalog.AllKeys.Where(k => k != PermissionCatalog.ManageRoles).ToArray(),
                "Move administration to the custom role."),
            CancellationToken.None);

        // …then deactivating the custom role would lock the tenant out.
        var act = () => new SetRoleActiveHandler(db).Handle(
            new SetRoleActiveCommand(custom.Id, Active: false), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("ROLE-006");
    }

    private static NT.QAMS.Infrastructure.Services.CurrentTenant CurrentTenantFor(Guid tenantId)
    {
        var tenant = new NT.QAMS.Infrastructure.Services.CurrentTenant();
        tenant.Set(tenantId);
        return tenant;
    }
}
