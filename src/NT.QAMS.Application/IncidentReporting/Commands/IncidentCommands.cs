using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Application.Compliance;
using NT.QAMS.Contracts.IncidentReporting;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.IncidentReporting;
using NT.QAMS.Domain.Improvement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.IncidentReporting.Commands;

// ── Report (attributed) ─────────────────────────────────────────────────────

[RequireInternalActor]
public sealed record ReportIncidentCommand(
    string Title, string Description, IncidentCategory Category, HarmGrade HarmGrade,
    IntakeChannel Channel, DateTimeOffset OccurredAtUtc, string? Location = null,
    Guid? BranchId = null, Guid? DepartmentId = null)
    : ICommand<Guid>;

public sealed class ReportIncidentValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}

public sealed class ReportIncidentHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs, IClock clock)
    : ICommandHandler<ReportIncidentCommand, Guid>
{
    public async Task<Guid> Handle(ReportIncidentCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        IncidentGuard.NotFuture(command.OccurredAtUtc, clock);

        var incidentRef = await refs.NextAsync(tenantId, "INC", cancellationToken);
        var incident = Incident.Report(
            incidentRef, command.Title, command.Description, command.Category, command.HarmGrade,
            command.Channel, command.OccurredAtUtc, actor, command.Location);
        incident.BranchId = command.BranchId;
        incident.DepartmentId = command.DepartmentId;

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);
        return incident.Id;
    }
}

// ── Report (anonymous) ──────────────────────────────────────────────────────

[RequireInternalActor]
public sealed record ReportAnonymousIncidentCommand(
    string Title, string Description, IncidentCategory Category, HarmGrade HarmGrade,
    IntakeChannel Channel, DateTimeOffset OccurredAtUtc, string? Location = null,
    Guid? BranchId = null, Guid? DepartmentId = null)
    : ICommand<AnonymousIncidentReceipt>;

public sealed class ReportAnonymousIncidentValidator : AbstractValidator<ReportAnonymousIncidentCommand>
{
    public ReportAnonymousIncidentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}

public sealed class ReportAnonymousIncidentHandler(
    IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs, IClock clock)
    : ICommandHandler<ReportAnonymousIncidentCommand, AnonymousIncidentReceipt>
{
    public async Task<AnonymousIncidentReceipt> Handle(
        ReportAnonymousIncidentCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");

        IncidentGuard.NotFuture(command.OccurredAtUtc, clock);

        // Mint the one-time follow-up reference here; the aggregate stores only its
        // hash, so identity and traceability co-exist without a reporter on the record.
        var followUp = AnonymousReference.NewCode();
        var incidentRef = await refs.NextAsync(tenantId, "INC", cancellationToken);
        var incident = Incident.ReportAnonymous(
            incidentRef, command.Title, command.Description, command.Category, command.HarmGrade,
            command.Channel, command.OccurredAtUtc, AnonymousReference.Hash(followUp), command.Location);
        incident.BranchId = command.BranchId;
        incident.DepartmentId = command.DepartmentId;

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);
        return new AnonymousIncidentReceipt(incident.Id, incident.IncidentRef, followUp);
    }
}

// ── Workflow transitions (load → guarded aggregate method → save) ────────────

[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Approve)]
public sealed record TriageIncidentCommand(Guid IncidentId, Guid AssigneeId, IncidentCategory Category) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Void)]
public sealed record RejectIncidentCommand(Guid IncidentId, string Reason) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Approve)]
public sealed record StartIncidentInvestigationCommand(Guid IncidentId, Guid InvestigatorId) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Edit)]
public sealed record AddContributingFactorCommand(
    Guid IncidentId, ContributingFactorCategory Category, string Description) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Edit)]
public sealed record AddTimelineEntryCommand(Guid IncidentId, DateTimeOffset OccurredAtUtc, string Note) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Edit)]
public sealed record RecordInvestigationSummaryCommand(Guid IncidentId, string Summary) : ICommand;
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Approve)]
public sealed record SubmitIncidentForReviewCommand(Guid IncidentId) : ICommand;

/// <summary>Closing an incident is a Part 11 signing ceremony: it requires the actor's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Sign)]
public sealed record CloseIncidentCommand(Guid IncidentId, string ClosureSummary, string Password, string Pin) : ICommand;

/// <summary>Declaring a sentinel event is a Part 11 signing ceremony: it requires the actor's e-signature (account password + PIN).</summary>
[RequirePermissionPolicy(PermissionCatalog.Incidents, PermissionAction.Sign)]
public sealed record DeclareSentinelCommand(Guid IncidentId, string Password, string Pin) : ICommand;

public sealed class RejectIncidentValidator : AbstractValidator<RejectIncidentCommand>
{
    public RejectIncidentValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public sealed class AddContributingFactorValidator : AbstractValidator<AddContributingFactorCommand>
{
    public AddContributingFactorValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
}

public sealed class AddTimelineEntryValidator : AbstractValidator<AddTimelineEntryCommand>
{
    public AddTimelineEntryValidator() => RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
}

public sealed class RecordInvestigationSummaryValidator : AbstractValidator<RecordInvestigationSummaryCommand>
{
    public RecordInvestigationSummaryValidator() => RuleFor(x => x.Summary).NotEmpty().MaximumLength(8000);
}

public sealed class CloseIncidentValidator : AbstractValidator<CloseIncidentCommand>
{
    public CloseIncidentValidator() => RuleFor(x => x.ClosureSummary).NotEmpty().MaximumLength(8000);
}

/// <summary>Shared load-or-throw for all incident transition handlers (tenant filter applies).</summary>
internal static class IncidentLoader
{
    public static async Task<Incident> LoadAsync(IAppDbContext db, Guid incidentId, CancellationToken ct) =>
        await db.Incidents
            .Include(i => i.ContributingFactors)
            .Include(i => i.Timeline)
            .SingleOrDefaultAsync(i => i.Id == incidentId, ct)
        ?? throw new DomainException("INC-404", "Incident not found.");
}

public sealed class TriageIncidentHandler(IAppDbContext db) : ICommandHandler<TriageIncidentCommand>
{
    public async Task Handle(TriageIncidentCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).Triage(c.AssigneeId, c.Category);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RejectIncidentHandler(IAppDbContext db) : ICommandHandler<RejectIncidentCommand>
{
    public async Task Handle(RejectIncidentCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).Reject(c.Reason);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class StartIncidentInvestigationHandler(IAppDbContext db)
    : ICommandHandler<StartIncidentInvestigationCommand>
{
    public async Task Handle(StartIncidentInvestigationCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).StartInvestigation(c.InvestigatorId);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class AddContributingFactorHandler(IAppDbContext db)
    : ICommandHandler<AddContributingFactorCommand>
{
    public async Task Handle(AddContributingFactorCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).AddContributingFactor(c.Category, c.Description);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class AddTimelineEntryHandler(IAppDbContext db, ICurrentUser user)
    : ICommandHandler<AddTimelineEntryCommand>
{
    public async Task Handle(AddTimelineEntryCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).AddTimelineEntry(c.OccurredAtUtc, c.Note, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RecordInvestigationSummaryHandler(IAppDbContext db)
    : ICommandHandler<RecordInvestigationSummaryCommand>
{
    public async Task Handle(RecordInvestigationSummaryCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).RecordInvestigationSummary(c.Summary);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SubmitIncidentForReviewHandler(IAppDbContext db)
    : ICommandHandler<SubmitIncidentForReviewCommand>
{
    public async Task Handle(SubmitIncidentForReviewCommand c, CancellationToken ct)
    {
        (await IncidentLoader.LoadAsync(db, c.IncidentId, ct)).SubmitForReview();
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CloseIncidentHandler(IAppDbContext db, ICurrentUser user, IESignatureService signatures)
    : ICommandHandler<CloseIncidentCommand>
{
    public async Task Handle(CloseIncidentCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var incident = await IncidentLoader.LoadAsync(db, c.IncidentId, ct);

        // Pre-validate every closure precondition BEFORE minting the signature — the
        // signature ledger is append-only, so a signature must never exist for a closure
        // that then fails its state or SoD gate (mirrors the NC verify/close ceremony).
        // The aggregate re-checks both invariants.
        if (incident.Status != IncidentStatus.PendingReview)
        {
            throw new InvalidStateTransitionException(
                "INC-024", $"Cannot close an incident in state {incident.Status}.");
        }

        if (incident.ReportedBy is { } reporter && reporter == actor)
        {
            throw new DomainException(
                "SOD-INC-001", "Segregation of duties: the reporter cannot close their own incident.");
        }

        // Bind the signature to the exact determination being attested (§11.70).
        var contentHash = SignatureContentHash.Compute(
            ("incident", incident.IncidentRef),
            ("title", incident.Title),
            ("harm", incident.HarmGrade.ToString()),
            ("sentinel", incident.IsSentinel ? "yes" : "no"),
            ("closure", c.ClosureSummary.Trim()));

        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Closed incident {incident.IncidentRef}",
            $"INC:{incident.Id:N}", contentHash, ct);

        incident.Close(c.ClosureSummary, actor);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DeclareSentinelHandler(
    IAppDbContext db, ICurrentUser user, IESignatureService signatures, IClock clock)
    : ICommandHandler<DeclareSentinelCommand>
{
    public async Task Handle(DeclareSentinelCommand c, CancellationToken ct)
    {
        var actor = user.UserId ?? throw new DomainException("AUTH-003", "An authenticated user is required.");
        var incident = await IncidentLoader.LoadAsync(db, c.IncidentId, ct);

        // Pre-validate before minting (append-only ledger; mirrors the NC ceremonies).
        if (incident.Status is IncidentStatus.Closed or IncidentStatus.Rejected)
        {
            throw new InvalidStateTransitionException(
                "INC-026", $"Cannot declare a sentinel event on an incident in state {incident.Status}.");
        }

        if (incident.IsSentinel)
        {
            throw new DomainException("INC-027", "This incident is already flagged as a sentinel event.");
        }

        await signatures.SignAsync(
            actor, c.Password, c.Pin,
            $"Declared sentinel event on {incident.IncidentRef}",
            $"INC:{incident.Id:N}",
            SignatureContentHash.Compute(
                ("incident", incident.IncidentRef), ("determination", "sentinel")), ct);

        incident.DeclareSentinel(actor, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// ── CAPA convergence — "one loop, many sources" (HQMS M03 universal intake) ──

/// <summary>
/// Raises a Nonconformance/CAPA record from an incident and back-links it, so a
/// finding converges into the single corrective-action pipeline with its origin
/// preserved. Creating a CAPA record is an NC.create act; the actor needs that
/// privilege. Idempotent: a second call returns the CAPA already linked.
/// </summary>
[RequirePermissionPolicy(PermissionCatalog.Nonconformances, PermissionAction.Create)]
public sealed record RaiseCapaFromIncidentCommand(Guid IncidentId) : ICommand<Guid>;

public sealed class RaiseCapaFromIncidentHandler(
    IAppDbContext db, ICurrentTenant tenant, ICurrentUser user, IReferenceNumberGenerator refs)
    : ICommandHandler<RaiseCapaFromIncidentCommand, Guid>
{
    public async Task<Guid> Handle(RaiseCapaFromIncidentCommand command, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var actor = user.UserId
            ?? throw new DomainException("AUTH-003", "An authenticated user is required.");

        var incident = await IncidentLoader.LoadAsync(db, command.IncidentId, ct);

        // Idempotency 1: the incident already points at its loop.
        if (incident.CorrectiveActionNcId is { } linked)
        {
            return linked;
        }

        // Idempotency 2: heal a partially applied earlier run (NC created, link not saved),
        // matched by the origin reference — mirrors ComplaintToNcPolicy.
        var sourceRef = $"INC:{incident.IncidentRef}";
        var existing = await db.Nonconformances.FirstOrDefaultAsync(n => n.SourceRef == sourceRef, ct);
        if (existing is not null)
        {
            incident.LinkCorrectiveAction(existing.Id);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var (severity, likelihood) = HarmToRisk(incident.HarmGrade);
        var ncRef = await refs.NextAsync(tenantId, "NC", ct);
        var nc = Nonconformance.Raise(
            ncRef,
            $"Incident {incident.IncidentRef}: {incident.Title}",
            incident.Description,
            severity, likelihood,
            NcSourceType.Incident, actor, sourceRef);
        nc.TenantId = tenantId;
        nc.BranchId = incident.BranchId;
        nc.DepartmentId = incident.DepartmentId;
        nc.Submit();

        db.Nonconformances.Add(nc);
        incident.LinkCorrectiveAction(nc.Id);
        await db.SaveChangesAsync(ct);
        return nc.Id;
    }

    /// <summary>
    /// Seeds the CAPA's initial risk assessment from the incident's harm grade so the
    /// corrective action inherits a proportionate priority (the RPN is refined during RCA).
    /// </summary>
    private static (int Severity, int Likelihood) HarmToRisk(HarmGrade harm) => harm switch
    {
        HarmGrade.NearMiss => (2, 2),
        HarmGrade.NoHarm => (2, 2),
        HarmGrade.Minor => (3, 2),
        HarmGrade.Moderate => (3, 3),
        HarmGrade.Severe => (4, 4),
        HarmGrade.Death => (5, 4),
        _ => (3, 3),
    };
}

// ── Anonymous reference helpers ─────────────────────────────────────────────

/// <summary>
/// Mints and hashes the one-time follow-up reference for anonymous reports. The code
/// is Crockford base-32 (no ambiguous characters) so it can be read back over the
/// phone; only its SHA-256 (lower-case hex) is ever persisted.
/// </summary>
internal static class AnonymousReference
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewCode()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return $"IR-{new string(chars)}";
    }

    public static string Hash(string code)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexStringLower(digest);
    }
}

internal static class IncidentGuard
{
    public static void NotFuture(DateTimeOffset occurredAtUtc, IClock clock)
    {
        // Allow a small clock-skew tolerance so a client a few seconds ahead is accepted.
        if (occurredAtUtc > clock.UtcNow.AddMinutes(5))
        {
            throw new DomainException("INC-005", "The time the event occurred cannot be in the future.");
        }
    }
}
