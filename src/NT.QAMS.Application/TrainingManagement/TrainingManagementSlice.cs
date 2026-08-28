using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NT.QAMS.Application.Abstractions;
using NT.QAMS.Contracts.TrainingManagement;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.SharedKernel.Abstractions;
using NT.QAMS.SharedKernel.Primitives;

namespace NT.QAMS.Application.TrainingManagement;

// ── Course commands ────────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Create)]
public sealed record DefineCourseCommand(
    string Title, TrainingCategory Category, string Description, decimal DurationHours, int? ValidityMonths, int PassMark)
    : ICommand<Guid>;

public sealed class DefineCourseValidator : AbstractValidator<DefineCourseCommand>
{
    public DefineCourseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.DurationHours).GreaterThan(0);
        RuleFor(x => x.PassMark).InclusiveBetween(0, 100);
    }
}

public sealed class DefineCourseHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<DefineCourseCommand, Guid>
{
    public async Task<Guid> Handle(DefineCourseCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var courseRef = await refs.NextAsync(tenantId, "CRS", ct);
        var course = TrainingCourse.Define(courseRef, c.Title, c.Category, c.Description, c.DurationHours, c.ValidityMonths, c.PassMark);
        db.TrainingCourses.Add(course);
        await db.SaveChangesAsync(ct);
        return course.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Edit)]
public sealed record UpdateCourseCommand(
    Guid CourseId, string Title, TrainingCategory Category, string Description, decimal DurationHours, int? ValidityMonths, int PassMark)
    : ICommand;

public sealed class UpdateCourseValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.DurationHours).GreaterThan(0);
        RuleFor(x => x.PassMark).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateCourseHandler(IAppDbContext db) : ICommandHandler<UpdateCourseCommand>
{
    public async Task Handle(UpdateCourseCommand c, CancellationToken ct)
    {
        var course = await LoadCourse(db, c.CourseId, ct);
        course.UpdateDetails(c.Title, c.Category, c.Description, c.DurationHours, c.ValidityMonths, c.PassMark);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<TrainingCourse> LoadCourse(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.TrainingCourses.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("CRS-404", "Course not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Approve)]
public sealed record ActivateCourseCommand(Guid CourseId) : ICommand;

public sealed class ActivateCourseHandler(IAppDbContext db) : ICommandHandler<ActivateCourseCommand>
{
    public async Task Handle(ActivateCourseCommand c, CancellationToken ct)
    {
        (await UpdateCourseHandler.LoadCourse(db, c.CourseId, ct)).Activate();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Void)]
public sealed record RetireCourseCommand(Guid CourseId) : ICommand;

public sealed class RetireCourseHandler(IAppDbContext db) : ICommandHandler<RetireCourseCommand>
{
    public async Task Handle(RetireCourseCommand c, CancellationToken ct)
    {
        (await UpdateCourseHandler.LoadCourse(db, c.CourseId, ct)).Retire();
        await db.SaveChangesAsync(ct);
    }
}

// ── Session commands ───────────────────────────────────────────────────────────

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Create)]
public sealed record ScheduleSessionCommand(
    Guid CourseId, DateTimeOffset ScheduledAtUtc, string Location, string TrainerName) : ICommand<Guid>;

public sealed class ScheduleSessionHandler(IAppDbContext db, ICurrentTenant tenant, IReferenceNumberGenerator refs)
    : ICommandHandler<ScheduleSessionCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleSessionCommand c, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new DomainException("TENANT-000", "A tenant context is required.");
        var course = await UpdateCourseHandler.LoadCourse(db, c.CourseId, ct);
        if (course.Status != CourseStatus.Active)
        {
            // M-20: a Draft course is still editable and a Retired one is history.
            throw new DomainException("CRS-013", "Only an active course can be delivered.");
        }

        var sessionRef = await refs.NextAsync(tenantId, "SES", ct);
        var session = TrainingSession.Schedule(c.CourseId, sessionRef, c.ScheduledAtUtc, c.Location, c.TrainerName);
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session.Id;
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Edit)]
public sealed record RegisterAttendeeCommand(Guid SessionId, Guid TraineeId) : ICommand;

public sealed class RegisterAttendeeHandler(IAppDbContext db) : ICommandHandler<RegisterAttendeeCommand>
{
    public async Task Handle(RegisterAttendeeCommand c, CancellationToken ct)
    {
        var session = await LoadSession(db, c.SessionId, ct);
        session.RegisterAttendee(c.TraineeId);
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<TrainingSession> LoadSession(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.TrainingSessions.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("SES-404", "Session not found.");
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Edit)]
public sealed record HoldSessionCommand(Guid SessionId) : ICommand;

public sealed class HoldSessionHandler(IAppDbContext db) : ICommandHandler<HoldSessionCommand>
{
    public async Task Handle(HoldSessionCommand c, CancellationToken ct)
    {
        var session = await RegisterAttendeeHandler.LoadSession(db, c.SessionId, ct);
        var course = await UpdateCourseHandler.LoadCourse(db, session.CourseId, ct);
        if (course.Status != CourseStatus.Active)
        {
            throw new DomainException("CRS-013", "Only an active course can be delivered.");
        }

        // M-20: freeze the pass threshold this delivery is judged against.
        session.Hold(course.PassMark);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Edit)]
public sealed record RecordAttendanceCommand(
    Guid SessionId, Guid TraineeId, bool Attended, int? PreScore, int? PostScore) : ICommand;

public sealed class RecordAttendanceHandler(IAppDbContext db) : ICommandHandler<RecordAttendanceCommand>
{
    public async Task Handle(RecordAttendanceCommand c, CancellationToken ct)
    {
        var session = await RegisterAttendeeHandler.LoadSession(db, c.SessionId, ct);
        // M-20: judged against the pass mark frozen at Hold, not the live course.
        session.RecordAttendance(c.TraineeId, c.Attended, c.PreScore, c.PostScore);
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Void)]
public sealed record CloseSessionCommand(Guid SessionId) : ICommand;

public sealed class CloseSessionHandler(IAppDbContext db) : ICommandHandler<CloseSessionCommand>
{
    public async Task Handle(CloseSessionCommand c, CancellationToken ct)
    {
        (await RegisterAttendeeHandler.LoadSession(db, c.SessionId, ct)).Close();
        await db.SaveChangesAsync(ct);
    }
}

[RequirePermissionPolicy(PermissionCatalog.Training, PermissionAction.Void)]
public sealed record CancelSessionCommand(Guid SessionId) : ICommand;

public sealed class CancelSessionHandler(IAppDbContext db) : ICommandHandler<CancelSessionCommand>
{
    public async Task Handle(CancelSessionCommand c, CancellationToken ct)
    {
        (await RegisterAttendeeHandler.LoadSession(db, c.SessionId, ct)).Cancel();
        await db.SaveChangesAsync(ct);
    }
}

// ── Queries ────────────────────────────────────────────────────────────────────

public sealed record GetCoursesQuery(string? Category = null, string? Status = null)
    : IQuery<IReadOnlyList<CourseListItemDto>>;

public sealed class GetCoursesHandler(IAppDbContext db) : IQueryHandler<GetCoursesQuery, IReadOnlyList<CourseListItemDto>>
{
    public async Task<IReadOnlyList<CourseListItemDto>> Handle(GetCoursesQuery q, CancellationToken ct)
    {
        var query = db.TrainingCourses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Category))
        {
            query = query.Where(c => c.Category.ToString() == q.Category);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(c => c.Status.ToString() == q.Status);
        }

        var courses = await query.OrderBy(c => c.Title).ToListAsync(ct);
        var counts = await db.TrainingSessions.AsNoTracking()
            .GroupBy(s => s.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct);

        return courses.Select(c => new CourseListItemDto(
            c.Id, c.CourseRef, c.Title, c.Category.ToString(), c.DurationHours, c.ValidityMonths, c.PassMark,
            c.Status.ToString(), counts.GetValueOrDefault(c.Id))).ToList();
    }
}

public sealed record GetCourseByIdQuery(Guid CourseId) : IQuery<CourseDetailDto>;

public sealed class GetCourseByIdHandler(IAppDbContext db) : IQueryHandler<GetCourseByIdQuery, CourseDetailDto>
{
    public async Task<CourseDetailDto> Handle(GetCourseByIdQuery q, CancellationToken ct)
    {
        var c = await db.TrainingCourses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.CourseId, ct)
            ?? throw new DomainException("CRS-404", "Course not found.");

        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.CourseId == q.CourseId && (s.Status == SessionStatus.Held || s.Status == SessionStatus.Closed))
            .Include(s => s.Attendance)
            .ToListAsync(ct);

        return new CourseDetailDto(
            c.Id, c.CourseRef, c.Title, c.Category.ToString(), c.Description, c.DurationHours,
            c.ValidityMonths, c.PassMark, c.Status.ToString(), Effectiveness(sessions));
    }

    internal static CourseEffectivenessDto Effectiveness(IReadOnlyCollection<TrainingSession> sessions)
    {
        var attended = sessions.SelectMany(s => s.Attendance).Where(a => a.Attended).ToList();
        var passed = attended.Count(a => a.Passed);
        var pre = attended.Where(a => a.PreScore.HasValue).Select(a => a.PreScore!.Value).ToList();
        var post = attended.Where(a => a.PostScore.HasValue).Select(a => a.PostScore!.Value).ToList();
        var gains = attended.Where(a => a.ScoreGain.HasValue).Select(a => a.ScoreGain!.Value).ToList();

        return new CourseEffectivenessDto(
            sessions.Count, attended.Count, passed,
            attended.Count == 0 ? 0m : decimal.Round(passed * 100m / attended.Count, 1),
            pre.Count == 0 ? null : decimal.Round((decimal)pre.Average(), 1),
            post.Count == 0 ? null : decimal.Round((decimal)post.Average(), 1),
            gains.Count == 0 ? null : decimal.Round((decimal)gains.Average(), 1));
    }
}

public sealed record GetSessionsQuery(Guid? CourseId = null, string? Status = null)
    : IQuery<IReadOnlyList<SessionListItemDto>>;

public sealed class GetSessionsHandler(IAppDbContext db) : IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionListItemDto>>
{
    public async Task<IReadOnlyList<SessionListItemDto>> Handle(GetSessionsQuery q, CancellationToken ct)
    {
        var query = db.TrainingSessions.AsNoTracking().Include(s => s.Attendance).AsQueryable();
        if (q.CourseId is { } courseId)
        {
            query = query.Where(s => s.CourseId == courseId);
        }

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            query = query.Where(s => s.Status.ToString() == q.Status);
        }

        var sessions = await query.OrderByDescending(s => s.ScheduledAtUtc).ToListAsync(ct);
        var titles = await db.TrainingCourses.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Title, ct);

        return sessions.Select(s => new SessionListItemDto(
            s.Id, s.CourseId, titles.GetValueOrDefault(s.CourseId, "—"), s.SessionRef, s.ScheduledAtUtc,
            s.Location, s.TrainerName, s.Status.ToString(), s.Attendance.Count, s.AttendedCount)).ToList();
    }
}

public sealed record GetSessionByIdQuery(Guid SessionId) : IQuery<SessionDetailDto>;

public sealed class GetSessionByIdHandler(IAppDbContext db) : IQueryHandler<GetSessionByIdQuery, SessionDetailDto>
{
    public async Task<SessionDetailDto> Handle(GetSessionByIdQuery q, CancellationToken ct)
    {
        var s = await db.TrainingSessions.AsNoTracking().Include(x => x.Attendance)
            .SingleOrDefaultAsync(x => x.Id == q.SessionId, ct)
            ?? throw new DomainException("SES-404", "Session not found.");

        var course = await db.TrainingCourses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == s.CourseId, ct);

        return new SessionDetailDto(
            s.Id, s.CourseId, course?.Title ?? "—", s.SessionRef, s.ScheduledAtUtc, s.Location, s.TrainerName,
            s.Status.ToString(), course?.PassMark ?? 0,
            s.Attendance.Select(a => new AttendanceDto(
                a.Id, a.TraineeId, a.Attended, a.PreScore, a.PostScore, a.ScoreGain, a.Passed)).ToList());
    }
}

/// <summary>
/// Training compliance dashboard (HQMS M12): for every active course, the delivery and effectiveness
/// roll-up — sessions held, distinct trainees reached, how many passed, the pass rate and mean
/// post-assessment score.
/// </summary>
public sealed record GetTrainingComplianceQuery : IQuery<IReadOnlyList<TrainingComplianceRowDto>>;

public sealed class GetTrainingComplianceHandler(IAppDbContext db, IClock clock)
    : IQueryHandler<GetTrainingComplianceQuery, IReadOnlyList<TrainingComplianceRowDto>>
{
    public async Task<IReadOnlyList<TrainingComplianceRowDto>> Handle(GetTrainingComplianceQuery q, CancellationToken ct)
    {
        var courses = await db.TrainingCourses.AsNoTracking()
            .Where(c => c.Status == CourseStatus.Active)
            .OrderBy(c => c.Title)
            .ToListAsync(ct);

        var sessions = await db.TrainingSessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Held || s.Status == SessionStatus.Closed)
            .Include(s => s.Attendance)
            .ToListAsync(ct);

        var byCourse = sessions.ToLookup(s => s.CourseId);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        return courses.Select(c =>
        {
            var courseSessions = byCourse[c.Id].ToList();
            var attended = courseSessions.SelectMany(s => s.Attendance).Where(a => a.Attended).ToList();
            var distinct = attended.Select(a => a.TraineeId).Distinct().Count();
            var passedTrainees = attended.Where(a => a.Passed).Select(a => a.TraineeId).Distinct().Count();
            var post = attended.Where(a => a.PostScore.HasValue).Select(a => a.PostScore!.Value).ToList();

            // M-20: currency — the dashboard's stated basis. A pass stays
            // current for ValidityMonths from its session date (latest pass
            // wins); a null validity never lapses (pattern:
            // CompetencyRecord.ExpiresAt).
            var current = 0;
            var lapsed = 0;
            foreach (var trainee in courseSessions
                         .SelectMany(s => s.Attendance.Where(a => a.Attended && a.Passed)
                             .Select(a => new { a.TraineeId, s.ScheduledAtUtc }))
                         .GroupBy(x => x.TraineeId))
            {
                if (c.ValidityMonths is not { } months)
                {
                    current++;
                    continue;
                }

                var latest = DateOnly.FromDateTime(trainee.Max(x => x.ScheduledAtUtc).UtcDateTime);
                if (latest.AddMonths(months) >= today) { current++; } else { lapsed++; }
            }

            return new TrainingComplianceRowDto(
                c.Id, c.CourseRef, c.Title, c.Category.ToString(), courseSessions.Count,
                distinct, passedTrainees,
                distinct == 0 ? 0m : decimal.Round(passedTrainees * 100m / distinct, 1),
                post.Count == 0 ? null : decimal.Round((decimal)post.Average(), 1),
                current, lapsed);
        }).ToList();
    }
}
