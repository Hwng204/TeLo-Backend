namespace Infrastructure.Models;

// Read models returned by the matrix repositories. The Application layer maps them to DTOs.

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record MatrixListFilter(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    ulong? AcademicContextId = null,
    ulong? SemesterId = null,
    string? Status = null,
    ulong? AssignedToUserId = null,
    ulong? BranchId = null);

public sealed record MatrixListRow(
    ulong Id,
    string Name,
    string Status,
    ulong? TaskId,
    ulong AcademicContextId,
    ulong? SemesterId,
    uint TotalQuestions,
    decimal TotalScore);

public sealed record MatrixTaskFilter(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    ulong? AssignedToUserId = null,
    DateTime? DueBefore = null,
    ulong? BranchId = null);

public sealed record MatrixTaskRow(
    ulong Id,
    ulong CreatedByUserId,
    ulong AssignedToUserId,
    DateTime? DueAt,
    string Status,
    string TaskType,
    string? Description,
    ulong? AcademicContextId,
    ulong? SemesterId,
    ulong? MatrixId);

public sealed record MatrixExportInfo(
    string ContextLabel,
    string? SemesterName,
    IReadOnlyDictionary<ulong, string> LessonTitles);

public sealed record MatrixAcademicContextOption(
    ulong Id,
    string Label,
    ulong AcademicYearId,
    ulong SchoolBranchId,
    ulong TextbookId,
    ulong SubjectId,
    ulong GradeLevelId,
    // Display names so a client can show each dimension in its own field
    // instead of parsing them back out of Label.
    string TextbookTitle = "",
    string SubjectName = "",
    string GradeLevelName = "",
    string AcademicYearName = "");

public sealed record MatrixSemesterOption(
    ulong Id,
    ulong AcademicYearId,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record MatrixLessonOption(
    ulong Id,
    ulong ContextId,
    ulong ChapterId,
    string Title,
    uint SortOrder);

public sealed record MatrixTeamLeadOption(
    ulong Id,
    string Username,
    string FullName,
    ulong? SchoolBranchId);

public sealed record MatrixReferenceModel(
    IReadOnlyList<MatrixAcademicContextOption> AcademicContexts,
    IReadOnlyList<MatrixSemesterOption> Semesters,
    IReadOnlyList<MatrixLessonOption> Lessons,
    IReadOnlyList<MatrixTeamLeadOption> TeamLeads);
