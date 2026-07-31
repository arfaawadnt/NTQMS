using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.ComplianceLedger;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.Infrastructure.Persistence;
using NT.QAMS.Infrastructure.Persistence.Interceptors;
using NT.QAMS.SharedKernel.Abstractions;
using Xunit;

namespace NT.QAMS.WebApi.FunctionalTests;

/// <summary>
/// Integration tests for the Part 11 field-level audit interceptor against a
/// real (in-memory) DbContext: create rows, per-property old/new capture on
/// modification, ledger self-exclusion, and credential redaction.
/// </summary>
public sealed class FieldChangeInterceptorTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedUser : ICurrentUser
    {
        public Guid? UserId => Actor;
        public string? DisplayName => "Field Tester";
        public bool IsAuthenticated => true;
        public NT.QAMS.Domain.IdentityAccess.UserRole? Role =>
            NT.QAMS.Domain.IdentityAccess.UserRole.QualityManager;
    }

    private static AppDbContext CreateContext(string name, string? reason = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .AddInterceptors(new FieldChangeInterceptor(
                new FixedClock(), new FixedUser(), new StubTenant(), new StubReason(reason)))
            .Options;
        return new AppDbContext(options, new StubTenant());
    }

    private sealed class StubTenant : ICurrentTenant
    {
        public Guid? TenantId => Tenant;
        public bool IsResolved => true;
        public bool IsElevated => false;
    }

    private sealed class StubReason(string? reason) : ICurrentChangeReason
    {
        public string? Reason => reason;
    }

    private static Complaint NewComplaint() => Complaint.Log(
        "CMP-2026-0100", ComplaintChannel.Email, "Reporter", null,
        confidential: false, "Subject", "Description", Actor, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Creating_a_record_writes_a_created_row()
    {
        await using var db = CreateContext(nameof(Creating_a_record_writes_a_created_row));
        var complaint = NewComplaint();
        complaint.TenantId = Tenant;

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var rows = await db.FieldChanges.Where(f => f.EntityType == nameof(Complaint)).ToListAsync();
        rows.Should().ContainSingle(f => f.Action == "Created"
            && f.EntityId == complaint.Id.ToString()
            && f.Actor == "Field Tester" && f.TenantId == Tenant);
    }

    [Fact]
    public async Task Modifying_a_record_captures_old_and_new_values_per_property()
    {
        await using var db = CreateContext(nameof(Modifying_a_record_captures_old_and_new_values_per_property));
        var complaint = NewComplaint();
        complaint.TenantId = Tenant;
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        complaint.Acknowledge(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var statusRow = await db.FieldChanges.SingleAsync(f =>
            f.Action == "Modified" && f.Property == nameof(Complaint.Status));
        statusRow.OldValue.Should().Be(nameof(ComplaintStatus.Logged));
        statusRow.NewValue.Should().Be(nameof(ComplaintStatus.Acknowledged));
    }

    [Fact]
    public async Task Ledger_rows_never_generate_rows_about_themselves()
    {
        await using var db = CreateContext(nameof(Ledger_rows_never_generate_rows_about_themselves));
        var complaint = NewComplaint();
        complaint.TenantId = Tenant;
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync(); // writes 1 FieldChangeRecord itself

        var selfRows = await db.FieldChanges
            .Where(f => f.EntityType == nameof(FieldChangeRecord)).CountAsync();
        selfRows.Should().Be(0);
    }

    [Fact]
    public async Task The_change_reason_in_scope_is_stamped_on_every_ledger_row()
    {
        // F-06: the operator-supplied justification (e.g. the X-Change-Reason on a
        // void) is captured contemporaneously in the same transaction as the change.
        const string reason = "Transcription error — value keyed against the wrong run.";
        await using var db = CreateContext(nameof(The_change_reason_in_scope_is_stamped_on_every_ledger_row), reason);
        var complaint = NewComplaint();
        complaint.TenantId = Tenant;

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var row = await db.FieldChanges.SingleAsync(f => f.EntityType == nameof(Complaint));
        row.Reason.Should().Be(reason);
    }

    [Fact]
    public async Task A_change_with_no_reason_in_scope_leaves_the_ledger_reason_null()
    {
        await using var db = CreateContext(nameof(A_change_with_no_reason_in_scope_leaves_the_ledger_reason_null));
        var complaint = NewComplaint();
        complaint.TenantId = Tenant;

        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var row = await db.FieldChanges.SingleAsync(f => f.EntityType == nameof(Complaint));
        row.Reason.Should().BeNull();
    }

    [Fact]
    public void Credential_bearing_property_names_are_flagged_for_redaction()
    {
        FieldChangeInterceptor.IsSensitive("PasswordHash").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("MfaSecret").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("SignaturePin").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("Title").Should().BeFalse();
    }

    /// <summary>
    /// An elevated write - startup seeding, provisioning - has no request tenant
    /// by definition. Before the fix, the interceptor read only
    /// <c>ITenantScoped</c>, which an owned child is not: it carries a shadow
    /// <c>TenantId</c> instead. The result was 19,296 privilege-detail rows
    /// stamped NULL and therefore invisible to the tenant whose privileges
    /// changed, because the field-change read filters on tenant. This pins the
    /// owner's tenant reaching the ledger through the shadow value.
    /// </summary>
    [Fact]
    public async Task An_owned_childs_change_is_attributed_to_the_owner_tenant_on_an_elevated_write()
    {
        var elevated = new ElevatedTenant();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(An_owned_childs_change_is_attributed_to_the_owner_tenant_on_an_elevated_write))
            .AddInterceptors(
                // Same order as production: the stamp must land before the ledger reads it.
                new TenantStampInterceptor(elevated),
                new FieldChangeInterceptor(new FixedClock(), new FixedUser(), elevated, new StubReason(null)))
            .Options;
        await using var db = new AppDbContext(options, elevated);

        // Exactly the shape that produced the NULLs: a seeded role with owned
        // permissions, written with no request tenant resolved.
        var role = Role.CreateSystem("ITest Seeded Role", null, ["nc.view", "nc.export"]);
        role.TenantId = Tenant;
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var childRows = await db.FieldChanges
            .Where(f => f.EntityType == nameof(RolePermission))
            .ToListAsync();

        childRows.Should().NotBeEmpty("the owned permissions are auditable changes");
        childRows.Should().OnlyContain(f => f.TenantId == Tenant,
            "an owned child's change belongs to the owner's tenant, and the field-change "
            + "read filters on tenant - a NULL here is invisible to the tenant it concerns");
    }

    /// <summary>An elevated unit of work: bypass on, no request tenant.</summary>
    private sealed class ElevatedTenant : ICurrentTenant
    {
        public Guid? TenantId => null;
        public bool IsResolved => false;
        public bool IsElevated => true;
    }
}
