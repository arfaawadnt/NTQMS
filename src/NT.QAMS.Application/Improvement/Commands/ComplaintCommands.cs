using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.Improvement;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.Improvement.Commands;

// â”€â”€ Log â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record LogComplaintCommand(
    ComplaintChannel Channel, string ComplainantName, string? ComplainantContact,
    bool Confidential, string Subject, string Description,
    Guid? BranchId = null, Guid? DepartmentId = null) : ICommand<Guid>;

public sealed class LogComplaintValidator : AbstractValidator<LogComplaintCommand>
{
    public LogComplaintValidator()
    {
        RuleFor(x => x.ComplainantName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ComplainantContact).MaximumLength(300);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

public sealed class LogComplaintHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user,
    IReferenceNumberGenerator refs, IClock clock)
    : ICommandHandler<LogComplaintCommand, Guid>
{
    public async Task<Guid> Handle(LogComplaintCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var complaintRef = await refs.NextAsync(tenantId, "CMP", cancellationToken);
        var complaint = Complaint.Log(
            complaintRef, command.Channel, command.ComplainantName, command.ComplainantContact,
            command.Confidential, command.Subject, command.Description, actor, clock.UtcNow);

        complaint.BranchId = command.BranchId;
        complaint.DepartmentId = command.DepartmentId;
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync(cancellationToken);
        return complaint.Id;
    }
}

// â”€â”€ Workflow transitions (load â†’ guarded aggregate method â†’ save) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[RequireInternalActor]
public sealed record AcknowledgeComplaintCommand(Guid ComplaintId) : ICommand;
[RequireInternalActor]
public sealed record ValidateComplaintCommand(Guid ComplaintId, bool Justified, string Reason) : ICommand;
[RequireInternalActor]
public sealed record StartComplaintInvestigationCommand(Guid ComplaintId) : ICommand;
[RequireInternalActor]
public sealed record LogComplaintOutcomeCommand(Guid ComplaintId, string Outcome) : ICommand;
[RequireInternalActor]
public sealed record ResolveComplaintCommand(Guid ComplaintId, string Resolution) : ICommand;
[RequireInternalActor]
public sealed record CloseComplaintCommand(Guid ComplaintId) : ICommand;

public sealed class ValidateComplaintValidator : AbstractValidator<ValidateComplaintCommand>
{
    public ValidateComplaintValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ComplaintWorkflowHandlers(IAppDbContext db, IClock clock) :
    ICommandHandler<AcknowledgeComplaintCommand>,
    ICommandHandler<ValidateComplaintCommand>,
    ICommandHandler<StartComplaintInvestigationCommand>,
    ICommandHandler<LogComplaintOutcomeCommand>,
    ICommandHandler<ResolveComplaintCommand>,
    ICommandHandler<CloseComplaintCommand>
{
    public async Task Handle(AcknowledgeComplaintCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);
        complaint.Acknowledge(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ValidateComplaintCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);
        complaint.RecordValidationVerdict(command.Justified, command.Reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(StartComplaintInvestigationCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);
        complaint.StartInvestigation();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(LogComplaintOutcomeCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);
        complaint.LogOutcome(command.Outcome);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ResolveComplaintCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);
        complaint.Resolve(command.Resolution);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CloseComplaintCommand command, CancellationToken ct)
    {
        var complaint = await LoadAsync(command.ComplaintId, ct);

        // Closure gate (CMP-020): same database, so the linked NC's live status
        // is checked transactionally rather than via an eventually consistent
        // projection.
        var linkedNcClosed = complaint.LinkedNcId is null
            || await db.Nonconformances
                .AnyAsync(n => n.Id == complaint.LinkedNcId && n.Status == NcStatus.Closed, ct);

        complaint.Close(linkedNcClosed);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Complaint> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Complaints.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new DomainException("CMP-404", "Complaint not found.");
}

// â”€â”€ Queries â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record GetComplaintsQuery(string? Status, bool CanViewConfidential)
    : IQuery<IReadOnlyList<ComplaintListItemDto>>;

public sealed class GetComplaintsHandler(IAppDbContext db)
    : IQueryHandler<GetComplaintsQuery, IReadOnlyList<ComplaintListItemDto>>
{
    public async Task<IReadOnlyList<ComplaintListItemDto>> Handle(
        GetComplaintsQuery query, CancellationToken ct)
    {
        var complaints = db.Complaints.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ComplaintStatus>(query.Status, ignoreCase: true, out var status))
        {
            complaints = complaints.Where(c => c.Status == status);
        }

        return await complaints
            .OrderByDescending(c => c.LoggedAtUtc)
            .Select(c => new ComplaintListItemDto(
                c.Id, c.ComplaintRef, c.Subject, c.Channel.ToString(), c.Status.ToString(),
                c.Confidential,
                c.Confidential && !query.CanViewConfidential ? "â€¢â€¢â€¢" : c.ComplainantName,
                c.LoggedAtUtc, c.BranchId, c.DepartmentId))
            .ToListAsync(ct);
    }
}

public sealed record GetComplaintByIdQuery(Guid ComplaintId, bool CanViewConfidential)
    : IQuery<ComplaintDetailDto>;

public sealed class GetComplaintByIdHandler(IAppDbContext db)
    : IQueryHandler<GetComplaintByIdQuery, ComplaintDetailDto>
{
    public async Task<ComplaintDetailDto> Handle(GetComplaintByIdQuery query, CancellationToken ct)
    {
        var c = await db.Complaints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.ComplaintId, ct)
            ?? throw new DomainException("CMP-404", "Complaint not found.");

        // Confidential reporter identity is masked for roles without the
        // view-confidential privilege (QM / TenantAdmin have it).
        var masked = c.Confidential && !query.CanViewConfidential;
        return new ComplaintDetailDto(
            c.Id, c.ComplaintRef, c.Channel.ToString(),
            masked ? "â€¢â€¢â€¢" : c.ComplainantName,
            masked ? null : c.ComplainantContact,
            c.Confidential, c.Subject, c.Description, c.Status.ToString(),
            c.LoggedAtUtc, c.AcknowledgedAtUtc, c.ValidationVerdict,
            c.InvestigationOutcome, c.Resolution, c.LinkedNcId);
    }
}
