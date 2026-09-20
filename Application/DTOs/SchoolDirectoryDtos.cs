namespace Application.DTOs;

public sealed record DirectoryPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record PageQuery(int Page = 1, int PageSize = 20);

public sealed record StudentListQuery(
    string? Search,
    ulong? GradeLevelId,
    ulong? ClassId,
    ulong? SchoolBranchId,
    string? Status,
    int Page = 1,
    int PageSize = 20);

public sealed record ClassListQuery(
    string? Search,
    ulong? GradeLevelId,
    ulong? AcademicYearId,
    ulong? SchoolBranchId,
    string? Status,
    int Page = 1,
    int PageSize = 20);

public sealed record StudentListItem(
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

public sealed record StudentAcademicHistoryItem(
    ulong AcademicYearId,
    string AcademicYearName,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong ClassId,
    string ClassName,
    ulong? HomeroomTeacherId,
    string? HomeroomTeacherName,
    string EnrollmentStatus);

public sealed record StudentCurrentClassDto(
    ulong AcademicYearId,
    string AcademicYearName,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong ClassId,
    string ClassName,
    ulong SchoolBranchId,
    string SchoolBranchName,
    ulong? HomeroomTeacherId,
    string? HomeroomTeacherName);

public sealed record StudentDetailDto(
    ulong Id,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly AdmissionDate,
    string Status,
    StudentCurrentClassDto? CurrentClass,
    IReadOnlyList<StudentAcademicHistoryItem> AcademicHistory);

public sealed record ClassListItem(
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

public sealed record ClassStudentItem(
    ulong StudentId,
    string StudentCode,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string ClassName,
    string StudentStatus);

public sealed record ClassDetailDto(
    ClassListItem Class,
    DirectoryPage<ClassStudentItem> Students);

public sealed record DirectoryOption(ulong Id, string? Code, string Name);

public sealed record SchoolDirectoryReferenceData(
    IReadOnlyList<DirectoryOption> SchoolBranches,
    IReadOnlyList<DirectoryOption> GradeLevels,
    IReadOnlyList<DirectoryOption> AcademicYears,
    IReadOnlyList<DirectoryOption> Classes,
    IReadOnlyList<string> StudentStatuses,
    IReadOnlyList<string> ClassStatuses);

public sealed record StudentScoreItem(
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
