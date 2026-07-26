using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
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
    }

    private static AppDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .AddInterceptors(new FieldChangeInterceptor(new FixedClock(), new FixedUser()))
            .Options;
        return new AppDbContext(options, new StubTenant());
    }

    private sealed class StubTenant : ICurrentTenant
    {
        public Guid? TenantId => Tenant;
        public bool IsResolved => true;
        public bool IsElevated => false;
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
    public void Credential_bearing_property_names_are_flagged_for_redaction()
    {
        FieldChangeInterceptor.IsSensitive("PasswordHash").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("MfaSecret").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("SignaturePin").Should().BeTrue();
        FieldChangeInterceptor.IsSensitive("Title").Should().BeFalse();
    }
}
