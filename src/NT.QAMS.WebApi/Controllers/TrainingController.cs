using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NT.QAMS.Application.TrainingManagement;
using NT.QAMS.Contracts.TrainingManagement;
using NT.QAMS.Domain.Authorization;
using NT.QAMS.Domain.TrainingManagement;
using NT.QAMS.WebApi.Authorization;

namespace NT.QAMS.WebApi.Controllers;

/// <summary>
/// Training management API (HQMS M12): the course catalogue, scheduled sessions with attendance
/// and pre/post effectiveness capture, and the compliance dashboard. Distinct from the individual
/// training-assignment work queue (api/training-assignments); both share the "training" permission.
/// </summary>
[ApiController]
[Route("api/training")]
[Authorize]
public sealed class TrainingController(ISender sender) : ControllerBase
{
    // ── Courses ────────────────────────────────────────────────────────────────
    [HttpGet("courses")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.View)]
    public async Task<IActionResult> ListCourses([FromQuery] string? category, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetCoursesQuery(category, status), ct));

    [HttpGet("courses/{id:guid}")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.View)]
    public async Task<IActionResult> GetCourse(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetCourseByIdQuery(id), ct));

    [HttpGet("compliance")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.View)]
    public async Task<IActionResult> Compliance(CancellationToken ct) =>
        Ok(await sender.Send(new GetTrainingComplianceQuery(), ct));

    [HttpPost("courses")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Create)]
    public async Task<IActionResult> DefineCourse(DefineCourseRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new DefineCourseCommand(
            request.Title, Enum.Parse<TrainingCategory>(request.Category, ignoreCase: true), request.Description,
            request.DurationHours, request.ValidityMonths, request.PassMark), ct);
        return CreatedAtAction(nameof(GetCourse), new { id }, new { id });
    }

    [HttpPut("courses/{id:guid}")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Edit)]
    public async Task<IActionResult> UpdateCourse(Guid id, UpdateCourseRequest request, CancellationToken ct)
    {
        await sender.Send(new UpdateCourseCommand(
            id, request.Title, Enum.Parse<TrainingCategory>(request.Category, ignoreCase: true), request.Description,
            request.DurationHours, request.ValidityMonths, request.PassMark), ct);
        return NoContent();
    }

    [HttpPost("courses/{id:guid}/activate")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Approve)]
    public async Task<IActionResult> ActivateCourse(Guid id, CancellationToken ct)
    {
        await sender.Send(new ActivateCourseCommand(id), ct);
        return NoContent();
    }

    [HttpPost("courses/{id:guid}/retire")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Void)]
    public async Task<IActionResult> RetireCourse(Guid id, CancellationToken ct)
    {
        await sender.Send(new RetireCourseCommand(id), ct);
        return NoContent();
    }

    // ── Sessions ───────────────────────────────────────────────────────────────
    [HttpGet("sessions")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.View)]
    public async Task<IActionResult> ListSessions([FromQuery] Guid? courseId, [FromQuery] string? status, CancellationToken ct) =>
        Ok(await sender.Send(new GetSessionsQuery(courseId, status), ct));

    [HttpGet("sessions/{id:guid}")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.View)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new GetSessionByIdQuery(id), ct));

    [HttpPost("sessions")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Create)]
    public async Task<IActionResult> ScheduleSession(ScheduleSessionRequest request, CancellationToken ct)
    {
        var id = await sender.Send(new ScheduleSessionCommand(
            request.CourseId, request.ScheduledAtUtc, request.Location, request.TrainerName), ct);
        return CreatedAtAction(nameof(GetSession), new { id }, new { id });
    }

    [HttpPost("sessions/{id:guid}/attendees")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Edit)]
    public async Task<IActionResult> Register(Guid id, RegisterAttendeeRequest request, CancellationToken ct)
    {
        await sender.Send(new RegisterAttendeeCommand(id, request.TraineeId), ct);
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/hold")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Edit)]
    public async Task<IActionResult> Hold(Guid id, CancellationToken ct)
    {
        await sender.Send(new HoldSessionCommand(id), ct);
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/attendance")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Edit)]
    public async Task<IActionResult> RecordAttendance(Guid id, RecordAttendanceRequest request, CancellationToken ct)
    {
        await sender.Send(new RecordAttendanceCommand(
            id, request.TraineeId, request.Attended, request.PreScore, request.PostScore), ct);
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/close")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Void)]
    public async Task<IActionResult> CloseSession(Guid id, CancellationToken ct)
    {
        await sender.Send(new CloseSessionCommand(id), ct);
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    [RequirePermission(PermissionCatalog.Training, PermissionAction.Void)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken ct)
    {
        await sender.Send(new CancelSessionCommand(id), ct);
        return NoContent();
    }
}
