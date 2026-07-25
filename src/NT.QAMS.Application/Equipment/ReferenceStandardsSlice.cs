using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Resources;
using NT.QAMS.Domain.Equipment;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Equipment;

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed record RegisterReferenceStandardCommand(
    string Name, string Type, string TraceableTo,
    string? Manufacturer, string? LotNumber, string? CertificateNumber,
    string? CertifiedValue, string? UncertaintyStatement,
    DateOnly ReceivedOn, DateOnly? ExpiresOn,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class RegisterReferenceStandardValidator : AbstractValidator<RegisterReferenceStandardCommand>
{
    public RegisterReferenceStandardValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.TraceableTo).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Manufacturer).MaximumLength(200);
        RuleFor(x => x.LotNumber).MaximumLength(100);
        RuleFor(x => x.CertificateNumber).MaximumLength(100);
        RuleFor(x => x.CertifiedValue).MaximumLength(200);
        RuleFor(x => x.UncertaintyStatement).MaximumLength(200);
    }
}

public sealed class RegisterReferenceStandardHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<RegisterReferenceStandardCommand, Guid>
{
    public async Task<Guid> Handle(RegisterReferenceStandardCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var standardRef = await refs.NextAsync(tenantId, "RS", ct);
        var standard = ReferenceStandard.Register(
            standardRef, c.Name, Enum.Parse<ReferenceStandardType>(c.Type, ignoreCase: true),
            c.TraceableTo, c.Manufacturer, c.LotNumber, c.CertificateNumber,
            c.CertifiedValue, c.UncertaintyStatement, c.ReceivedOn, c.ExpiresOn);
        standard.BranchId = c.BranchId;
        standard.DepartmentId = c.DepartmentId;
        db.ReferenceStandards.Add(standard);
        await db.SaveChangesAsync(ct);
        return standard.Id;
    }
}

public sealed record QuarantineReferenceStandardCommand(Guid StandardId, string Reason) : ICommand;
public sealed record ReactivateReferenceStandardCommand(Guid StandardId) : ICommand;
public sealed record RetireReferenceStandardCommand(Guid StandardId) : ICommand;

public sealed class QuarantineReferenceStandardValidator : AbstractValidator<QuarantineReferenceStandardCommand>
{
    public QuarantineReferenceStandardValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReferenceStandardWorkflowHandlers(IAppDbContext db, IClock clock) :
    ICommandHandler<QuarantineReferenceStandardCommand>,
    ICommandHandler<ReactivateReferenceStandardCommand>,
    ICommandHandler<RetireReferenceStandardCommand>
{
    public async Task Handle(QuarantineReferenceStandardCommand c, CancellationToken ct)
    {
        var standard = await LoadAsync(c.StandardId, ct);
        standard.Quarantine(c.Reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ReactivateReferenceStandardCommand c, CancellationToken ct)
    {
        var standard = await LoadAsync(c.StandardId, ct);
        standard.Reactivate(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RetireReferenceStandardCommand c, CancellationToken ct)
    {
        var standard = await LoadAsync(c.StandardId, ct);
        standard.Retire();
        await db.SaveChangesAsync(ct);
    }

    private async Task<ReferenceStandard> LoadAsync(Guid id, CancellationToken ct) =>
        await db.ReferenceStandards.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new DomainException("RS-404", "Reference standard not found.");
}

// ── Queries ──────────────────────────────────────────────────────────────────

public sealed record GetReferenceStandardsQuery(string? Status)
    : IQuery<IReadOnlyList<ReferenceStandardListItemDto>>;

public sealed class GetReferenceStandardsHandler(IAppDbContext db)
    : IQueryHandler<GetReferenceStandardsQuery, IReadOnlyList<ReferenceStandardListItemDto>>
{
    public async Task<IReadOnlyList<ReferenceStandardListItemDto>> Handle(
        GetReferenceStandardsQuery q, CancellationToken ct)
    {
        var standards = db.ReferenceStandards.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<ReferenceStandardStatus>(q.Status, ignoreCase: true, out var status))
        {
            standards = standards.Where(s => s.Status == status);
        }

        return await standards
            .OrderByDescending(s => s.StandardRef)
            .Select(s => new ReferenceStandardListItemDto(
                s.Id, s.StandardRef, s.Name, s.Type.ToString(), s.TraceableTo,
                s.Status.ToString(), s.ExpiresOn, s.BranchId, s.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetReferenceStandardByIdQuery(Guid StandardId) : IQuery<ReferenceStandardDetailDto>;

public sealed class GetReferenceStandardByIdHandler(IAppDbContext db)
    : IQueryHandler<GetReferenceStandardByIdQuery, ReferenceStandardDetailDto>
{
    public async Task<ReferenceStandardDetailDto> Handle(GetReferenceStandardByIdQuery q, CancellationToken ct)
    {
        var s = await db.ReferenceStandards.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.StandardId, ct)
            ?? throw new DomainException("RS-404", "Reference standard not found.");

        return new ReferenceStandardDetailDto(
            s.Id, s.StandardRef, s.Name, s.Type.ToString(), s.TraceableTo,
            s.Manufacturer, s.LotNumber, s.CertificateNumber,
            s.CertifiedValue, s.UncertaintyStatement,
            s.ReceivedOn, s.ExpiresOn, s.Status.ToString(), s.QuarantineReason,
            s.BranchId, s.DepartmentId);
    }
}
