namespace Infrastructure.Repositories.Interface;

// School scope of a directory request. SchoolId set means an admin picked the school explicitly in
// the route; null means the school is derived from the signed-in actor's branch.
public sealed record DirectoryScope(ulong? ActorUserId, ulong? SchoolId)
{
    public static DirectoryScope FromActor(ulong actorUserId) => new(actorUserId, null);

    public static DirectoryScope ForSchool(ulong schoolId) => new(null, schoolId);
}

public sealed record StudentDirectoryFilter(
    DirectoryScope Scope,
    string? Search,
    ulong? GradeLevelId,
    ulong? ClassId,
    ulong? SchoolBranchId,
    string? Status,
    int Page,
    int PageSize);

public sealed record ClassDirectoryFilter(
    DirectoryScope Scope,
    string? Search,
    ulong? GradeLevelId,
    ulong? AcademicYearId,
    ulong? SchoolBranchId,
    string? Status,
    int Page,
    int PageSize);

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
    string Status,
    DateOnly AdmissionDate);

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
    DateOnly AdmissionDate,
    string Status,
    // Ordered by academic year start date DESC, then enrollment id DESC.
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
    string StudentStatus);

public sealed record ClassDetailRow(
    ClassDirectoryRow Class,
    DirectoryRowsPage<ClassStudentRow> Students);

public sealed record DirectoryOptionRow(ulong Id, string? Code, string Name);

public sealed record DirectoryReferenceRows(
    IReadOnlyList<DirectoryOptionRow> SchoolBranches,
    IReadOnlyList<DirectoryOptionRow> GradeLevels,
    IReadOnlyList<DirectoryOptionRow> AcademicYears,
    IReadOnlyList<DirectoryOptionRow> Classes);

// Read-only class/student directory. Every query is scoped to one school: either the actor's own
// school (users.school_branch_id -> school) or the school an admin named in the route.
public interface ISchoolDirectoryRepository
{
    Task<DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>> ListStudentsAsync(
        StudentDirectoryFilter filter,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope scope,
        ulong studentId,
        ulong classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<ClassDetailRow>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        int rosterPage,
        int rosterPageSize,
        CancellationToken cancellationToken);

    Task<DirectoryReadResult<DirectoryReferenceRows>> GetReferenceDataAsync(
        DirectoryScope scope,
        ulong? academicYearId,
        CancellationToken cancellationToken);
}
