namespace NT.QAMS.Contracts.TrainingManagement;

// ── Courses (catalogue) ───────────────────────────────────────────────────────

public sealed record DefineCourseRequest(
    string Title, string Category, string Description, decimal DurationHours, int? ValidityMonths, int PassMark);

public sealed record UpdateCourseRequest(
    string Title, string Category, string Description, decimal DurationHours, int? ValidityMonths, int PassMark);

public sealed record CourseListItemDto(
    Guid Id, string CourseRef, string Title, string Category, decimal DurationHours,
    int? ValidityMonths, int PassMark, string Status, int SessionCount);

public sealed record CourseEffectivenessDto(
    int SessionsHeld, int AttendedCount, int PassedCount, decimal PassRate,
    decimal? MeanPreScore, decimal? MeanPostScore, decimal? MeanGain);

public sealed record CourseDetailDto(
    Guid Id, string CourseRef, string Title, string Category, string Description, decimal DurationHours,
    int? ValidityMonths, int PassMark, string Status, CourseEffectivenessDto Effectiveness);

// ── Sessions (delivery + attendance) ──────────────────────────────────────────

public sealed record ScheduleSessionRequest(
    Guid CourseId, DateTimeOffset ScheduledAtUtc, string Location, string TrainerName);

public sealed record RegisterAttendeeRequest(Guid TraineeId);

public sealed record RecordAttendanceRequest(Guid TraineeId, bool Attended, int? PreScore, int? PostScore);

public sealed record SessionListItemDto(
    Guid Id, Guid CourseId, string CourseTitle, string SessionRef, DateTimeOffset ScheduledAtUtc,
    string Location, string TrainerName, string Status, int RegisteredCount, int AttendedCount);

public sealed record AttendanceDto(
    Guid Id, Guid TraineeId, bool Attended, int? PreScore, int? PostScore, int? ScoreGain, bool Passed);

public sealed record SessionDetailDto(
    Guid Id, Guid CourseId, string CourseTitle, string SessionRef, DateTimeOffset ScheduledAtUtc,
    string Location, string TrainerName, string Status, int PassMark, IReadOnlyList<AttendanceDto> Attendance);

// ── Compliance dashboard ──────────────────────────────────────────────────────

public sealed record TrainingComplianceRowDto(
    Guid CourseId, string CourseRef, string Title, string Category, int SessionsHeld,
    int DistinctTrainees, int PassedTrainees, decimal PassRate, decimal? MeanPostScore,
    int CurrentTrainees, int LapsedTrainees);
