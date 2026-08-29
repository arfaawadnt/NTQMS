using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Credentialing;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.Credentialing;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Credentialing;

// ── Commands ─────────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Create)]
public sealed record RegisterPractitionerCommand(string FullName, string Specialty) : ICommand<Guid>;

public sealed class RegisterPractitionerValidator : AbstractValidator<RegisterPractitionerCommand>
{
    public RegisterPractitionerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Specialty).NotEmpty().MaximumLength(150);
    }
}

public sealed class RegisterPractitionerHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterPractitionerCommand, Guid>
{
    public async Task<Guid> Handle(RegisterPractitionerCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var practitionerRef = await refs.NextAsync(tenantId, "PRC", ct);
        var practitioner = Practitioner.Register(practitionerRef, c.FullName, c.Specialty);
        db.Practitioners.Add(practitioner);
        await db.SaveChangesAsync(ct);
        return practitioner.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Edit)]
public sealed record AddLicenceCommand(
    Guid PractitionerId, CredentialType Type, string Identifier, string Issuer, DateOnly ExpiresOn) : ICommand<Guid>;

public sealed class AddLicenceValidator : AbstractValidator<AddLicenceCommand>
{
    public AddLicenceValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Issuer).MaximumLength(150);
    }
}

public sealed class AddLicenceHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<AddLicenceCommand, Guid>
{
    public async Task<Guid> Handle(AddLicenceCommand c, CancellationToken ct)
    {
        var practitioner = await Load(db, c.PractitionerId, ct);
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var id = practitioner.AddLicence(c.Type, c.Identifier, c.Issuer, c.ExpiresOn, actor);
        await db.SaveChangesAsync(ct);
        return id;
    }

    internal static async Task<Practitioner> Load(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Practitioners.Include(p => p.Licences).Include(p => p.Privileges).SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new DomainException("CRD-404", "Practitioner not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Approve)]
public sealed record VerifyLicenceCommand(Guid PractitionerId, Guid LicenceId, string Source) : ICommand;

public sealed class VerifyLicenceValidator : AbstractValidator<VerifyLicenceCommand>
{
    public VerifyLicenceValidator() => RuleFor(x => x.Source).NotEmpty().MaximumLength(300);
}

public sealed class VerifyLicenceHandler(IAppDbContext db, ICurrentUser user, IClock clock) : ICommandHandler<VerifyLicenceCommand>
{
    public async Task Handle(VerifyLicenceCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var practitioner = await AddLicenceHandler.Load(db, c.PractitionerId, ct);
        practitioner.VerifyLicence(c.LicenceId, actor, c.Source, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Edit)]
public sealed record RequestPrivilegeCommand(Guid PractitionerId, string Name) : ICommand<Guid>;

public sealed class RequestPrivilegeValidator : AbstractValidator<RequestPrivilegeCommand>
{
    public RequestPrivilegeValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public sealed class RequestPrivilegeHandler(IAppDbContext db, IClock clock) : ICommandHandler<RequestPrivilegeCommand, Guid>
{
    public async Task<Guid> Handle(RequestPrivilegeCommand c, CancellationToken ct)
    {
        var practitioner = await AddLicenceHandler.Load(db, c.PractitionerId, ct);
        var id = practitioner.RequestPrivilege(c.Name, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
        return id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Approve)]
public sealed record GrantPrivilegeCommand(Guid PractitionerId, Guid PrivilegeId, DateOnly? GrantedUntil) : ICommand;

public sealed class GrantPrivilegeHandler(IAppDbContext db) : ICommandHandler<GrantPrivilegeCommand>
{
    public async Task Handle(GrantPrivilegeCommand c, CancellationToken ct)
    {
        var practitioner = await AddLicenceHandler.Load(db, c.PractitionerId, ct);
        practitioner.GrantPrivilege(c.PrivilegeId, c.GrantedUntil);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Approve)]
public sealed record DenyPrivilegeCommand(Guid PractitionerId, Guid PrivilegeId, string Reason) : ICommand;

public sealed class DenyPrivilegeValidator : AbstractValidator<DenyPrivilegeCommand>
{
    public DenyPrivilegeValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class DenyPrivilegeHandler(IAppDbContext db) : ICommandHandler<DenyPrivilegeCommand>
{
    public async Task Handle(DenyPrivilegeCommand c, CancellationToken ct)
    {
        var practitioner = await AddLicenceHandler.Load(db, c.PractitionerId, ct);
        practitioner.DenyPrivilege(c.PrivilegeId, c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Approve)]
public sealed record CredentialPractitionerCommand(Guid PractitionerId, DateOnly AppointedUntil) : ICommand;

public sealed class CredentialPractitionerHandler(IAppDbContext db, IClock clock) : ICommandHandler<CredentialPractitionerCommand>
{
    public async Task Handle(CredentialPractitionerCommand c, CancellationToken ct)
    {
        (await AddLicenceHandler.Load(db, c.PractitionerId, ct)).Credential(c.AppointedUntil, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Approve)]
public sealed record ReappointPractitionerCommand(Guid PractitionerId, DateOnly AppointedUntil) : ICommand;

public sealed class ReappointPractitionerHandler(IAppDbContext db, IClock clock) : ICommandHandler<ReappointPractitionerCommand>
{
    public async Task Handle(ReappointPractitionerCommand c, CancellationToken ct)
    {
        (await AddLicenceHandler.Load(db, c.PractitionerId, ct)).Reappoint(c.AppointedUntil, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Void)]
public sealed record SuspendPractitionerCommand(Guid PractitionerId, string Reason) : ICommand;

public sealed class SuspendPractitionerValidator : AbstractValidator<SuspendPractitionerCommand>
{
    public SuspendPractitionerValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class SuspendPractitionerHandler(IAppDbContext db) : ICommandHandler<SuspendPractitionerCommand>
{
    public async Task Handle(SuspendPractitionerCommand c, CancellationToken ct)
    {
        (await AddLicenceHandler.Load(db, c.PractitionerId, ct)).Suspend(c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

// N-14: reinstating a practitioner to clinical practice is a governance act
// equal to suspending — gated with the same Void authority, not the lower Edit.
[RequirePermissionPolicy(PermissionCatalog.Credentialing, PermissionAction.Void)]
public sealed record ReinstatePractitionerCommand(Guid PractitionerId) : ICommand;

public sealed class ReinstatePractitionerHandler(IAppDbContext db) : ICommandHandler<ReinstatePractitionerCommand>
{
    public async Task Handle(ReinstatePractitionerCommand c, CancellationToken ct)
    {
        (await AddLicenceHandler.Load(db, c.PractitionerId, ct)).Reinstate();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetPractitionersQuery(string? Specialty = null, string? Status = null)
    : IQuery<IReadOnlyList<PractitionerListItemDto>>;

public sealed class GetPractitionersHandler(IAppDbContext db)
    : IQueryHandler<GetPractitionersQuery, IReadOnlyList<PractitionerListItemDto>>
{
    public async Task<IReadOnlyList<PractitionerListItemDto>> Handle(GetPractitionersQuery q, CancellationToken ct)
    {
        // M-10: server-side projection — the register needs two counts per
        // practitioner, not every licence and privilege materialized.
        var query = db.Practitioners.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Specialty))
        {
            query = query.Where(p => p.Specialty == q.Specialty);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(p => p.Status.ToString() == q.Status);
        }

        return await query.OrderBy(p => p.FullName)
            .Select(p => new PractitionerListItemDto(
                p.Id, p.PractitionerRef, p.FullName, p.Specialty, p.Status.ToString(), p.AppointedUntil,
                p.Privileges.Count(x => x.Status == PrivilegeStatus.Granted),
                p.Licences.Count(l => l.VerificationStatus == VerificationStatus.Verified)))
            .ToListAsync(ct);
    }
}

public sealed record GetPractitionerByIdQuery(Guid PractitionerId) : IQuery<PractitionerDetailDto>;

public sealed class GetPractitionerByIdHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetPractitionerByIdQuery, PractitionerDetailDto>
{
    public async Task<PractitionerDetailDto> Handle(GetPractitionerByIdQuery q, CancellationToken ct)
    {
        var p = await db.Practitioners.AsNoTracking().Include(x => x.Licences).Include(x => x.Privileges)
            .SingleOrDefaultAsync(x => x.Id == q.PractitionerId, ct)
            ?? throw new DomainException("CRD-404", "Practitioner not found.");

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return new PractitionerDetailDto(
            p.Id, p.PractitionerRef, p.FullName, p.Specialty, p.Status.ToString(), p.AppointedUntil, p.SuspensionReason,
            p.Licences.Select(l => new LicenceDto(
                l.Id, l.Type.ToString(), l.Identifier, l.Issuer, l.ExpiresOn, l.IsExpired(today),
                l.VerificationStatus.ToString(), l.VerifiedBy, l.VerificationSource, l.VerifiedAtUtc)).ToList(),
            p.Privileges.Select(x => new PrivilegeDto(
                x.Id, x.Name, x.Status.ToString(), x.GrantedUntil, x.DenialReason)).ToList());
    }
}

/// <summary>
/// The licence-expiry register (HQMS M13): every licence with its days-to-expiry and a tier —
/// Expired, Critical (≤30 days), Warning (≤90 days) or Ok — so renewals can be chased before lapse.
/// </summary>
public sealed record GetExpiringCredentialsQuery(int WithinDays = 90) : IQuery<IReadOnlyList<ExpiringCredentialDto>>;

public sealed class GetExpiringCredentialsHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetExpiringCredentialsQuery, IReadOnlyList<ExpiringCredentialDto>>
{
    public async Task<IReadOnlyList<ExpiringCredentialDto>> Handle(GetExpiringCredentialsQuery q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var cutoff = today.AddDays(Math.Clamp(q.WithinDays, 1, 730));

        // M-10: filter and flatten server-side — only the expiring rows travel,
        // not every practitioner with every licence.
        var expiring = await db.Practitioners.AsNoTracking()
            .SelectMany(p => p.Licences
                .Where(l => l.ExpiresOn <= cutoff)
                .Select(l => new
                {
                    p.Id, p.PractitionerRef, p.FullName,
                    LicenceId = l.Id, l.Type, l.Identifier, l.ExpiresOn,
                }))
            .OrderBy(x => x.ExpiresOn)
            .ToListAsync(ct);

        return expiring
            .Select(x =>
            {
                var days = x.ExpiresOn.DayNumber - today.DayNumber;
                var tier = days < 0 ? "Expired" : days <= 30 ? "Critical" : days <= 90 ? "Warning" : "Ok";
                return new ExpiringCredentialDto(
                    x.Id, x.PractitionerRef, x.FullName, x.LicenceId, x.Type.ToString(),
                    x.Identifier, x.ExpiresOn, days, tier);
            })
            .ToList();
    }
}

/// <summary>
/// Point-of-care privilege verification (HQMS M13): does this practitioner hold the named privilege
/// as an active grant today? The check that a ward or theatre system calls before a procedure.
/// </summary>
public sealed record VerifyPrivilegeQuery(Guid PractitionerId, string PrivilegeName) : IQuery<PrivilegeCheckResultDto>;

public sealed class VerifyPrivilegeHandler(IAppDbContext db, IClock clock) : IQueryHandler<VerifyPrivilegeQuery, PrivilegeCheckResultDto>
{
    public async Task<PrivilegeCheckResultDto> Handle(VerifyPrivilegeQuery q, CancellationToken ct)
    {
        var p = await db.Practitioners.AsNoTracking().Include(x => x.Privileges)
            .SingleOrDefaultAsync(x => x.Id == q.PractitionerId, ct)
            ?? throw new DomainException("CRD-404", "Practitioner not found.");

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var holds = p.HasActivePrivilege(q.PrivilegeName, today);
        var detail = holds
            ? null
            : p.Status != PractitionerStatus.Credentialed
                ? $"Practitioner is {p.Status}."
                : p.AppointedUntil is { } until && until < today
                    ? $"Appointment lapsed on {until:yyyy-MM-dd}."
                    : "No active grant for that privilege.";

        return new PrivilegeCheckResultDto(
            p.Id, p.PractitionerRef, p.FullName, q.PrivilegeName.Trim(), holds, p.Status.ToString(), detail);
    }
}
