namespace Infrastructure.Repositories.Interface;

// School scope of a directory request. SchoolId set means an admin picked the school explicitly in
// the route; null means the school is derived from the signed-in actor's branch.
// HomeroomOnly narrows a school-side actor (a plain teacher) to the class they lead and its students.
public sealed record DirectoryScope(ulong? ActorUserId, ulong? SchoolId, bool HomeroomOnly = false)
{
    public static DirectoryScope FromActor(ulong actorUserId, bool homeroomOnly = false) =>
        new(actorUserId, null, homeroomOnly);

    public static DirectoryScope ForSchool(ulong schoolId) => new(null, schoolId);
}

public enum DirectoryReadStatus
{
    Success,
    ActorNotFound,
    SchoolScopeMissing,
    SchoolNotFound,
    ActiveAcademicYearNotFound,
    NotFound
}

public sealed record DirectoryReadResult<T>(DirectoryReadStatus Status, T? Value)
{
    public static DirectoryReadResult<T> Ok(T value) => new(DirectoryReadStatus.Success, value);
    public static DirectoryReadResult<T> Fail(DirectoryReadStatus status) => new(status, default);
}

public sealed record DirectoryRowsPage<T>(IReadOnlyList<T> Items, int TotalCount);

public enum DirectoryWriteStatus
{
    Success,
    SchoolNotFound,
    StudentNotFound,
    ClassNotFound,
    BranchNotInSchool,
    AcademicYearInvalid,
    GradeLevelNotFound,
    DuplicateStudentCode,
    DuplicateClassCode,
    DuplicateClassName,
    TeacherNotInSchool,
    TeacherAlreadyHomeroom,
    ClassHasActiveStudents,
    StudentNotEnrolledInYear,
    UseClassTransfer,
    InvalidEffectiveDate
}

public sealed record DirectoryWriteResult<T>(DirectoryWriteStatus Status, T? Value)
{
    public static DirectoryWriteResult<T> Ok(T value) => new(DirectoryWriteStatus.Success, value);
    public static DirectoryWriteResult<T> Fail(DirectoryWriteStatus status) => new(status, default);
}

// ─── STUDENT MODELS ──────────────────────────────────────────────────────────

public sealed record StudentDirectoryFilter(
    DirectoryScope Scope,
    string? Search,
    ulong? GradeLevelId,
    ulong? ClassId,
    ulong? SchoolBranchId,
    string? Status,
    int Page,
    int PageSize,
    ulong? AcademicYearId = null);

public sealed record StudentDirectoryRow(
    ulong Id,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    ulong? GradeLevelId,
    string? GradeLevelName,
    ulong? ClassId,
    string? ClassName,
    ulong? SchoolBranchId,
    string? SchoolBranchName,
    string Status);

public sealed record StudentEnrollmentRow(
    ulong AcademicYearId,
    string AcademicYearName,
    string AcademicYearStatus,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong ClassId,
    string ClassName,
    ulong SchoolBranchId,
    string SchoolBranchName,
    ulong? HomeroomTeacherId,
    string? HomeroomTeacherName,
    string EnrollmentStatus);

public sealed record StudentDetailRow(
    ulong Id,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Status,
    IReadOnlyList<StudentEnrollmentRow> History);

public sealed record StudentScoreRow(
    ulong AttemptId,
    ulong ExamId,
    string ExamName,
    ulong SemesterId,
    string SemesterName,
    ulong SubjectId,
    string SubjectName,
    DateOnly ExamDate,
    decimal TotalScore,
    DateTime ResultPublishedAt);

public sealed record CreateStudentCommand(
    ulong SchoolId,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Status,
    ulong SchoolClassId);

public sealed record UpdateStudentCommand(
    ulong SchoolId,
    ulong StudentId,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string Status,
    ulong? SchoolClassId);

public sealed record TransferStudentClassCommand(
    ulong SchoolId,
    ulong StudentId,
    ulong SchoolClassId,
    DateOnly? EffectiveOn);

// ─── CLASS MODELS ────────────────────────────────────────────────────────────

public sealed record ClassDirectoryFilter(
    DirectoryScope Scope,
    string? Search,
    ulong? GradeLevelId,
    ulong? AcademicYearId,
    ulong? SchoolBranchId,
    string? Status,
    int Page,
    int PageSize);

public sealed record ClassDirectoryRow(
    ulong Id,
    string Code,
    string Name,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong AcademicYearId,
    string AcademicYearName,
    ulong SchoolBranchId,
    string SchoolBranchName,
    ulong? HomeroomTeacherId,
    string? HomeroomTeacherName,
    int StudentCount,
    string Status);

public sealed record ClassStudentRow(
    ulong StudentId,
    string StudentCode,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string ClassName,
    string StudentStatus,
    string EnrollmentStatus);

public sealed record ClassDetailRow(
    ClassDirectoryRow Class,
    DirectoryRowsPage<ClassStudentRow> Students);

public sealed record CreateClassCommand(
    ulong SchoolId,
    ulong SchoolBranchId,
    string Code,
    string Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string Status,
    ulong? HomeroomTeacherId);

public sealed record UpdateClassCommand(
    ulong SchoolId,
    ulong ClassId,
    ulong SchoolBranchId,
    string Code,
    string Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string Status,
    ulong? HomeroomTeacherId);

// ─── REFERENCE / SCHOOL MODELS ───────────────────────────────────────────────

public sealed record DirectoryOptionRow(ulong Id, string? Code, string Name);

public sealed record DirectoryBranchRow(
    ulong Id, string? Code, string Name, string Status, int ClassCount, int StudentCount);

public sealed record DirectoryReferenceRows(
    IReadOnlyList<DirectoryBranchRow> SchoolBranches,
    IReadOnlyList<DirectoryOptionRow> GradeLevels,
    IReadOnlyList<DirectoryOptionRow> AcademicYears,
    IReadOnlyList<DirectoryOptionRow> Classes);
