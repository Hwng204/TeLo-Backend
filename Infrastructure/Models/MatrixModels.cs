using Domain.Entities.QuestionBank;

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
    // MatrixScope.Key (the API's academicContextId).
    ulong? AcademicContextId = null,
    string? Status = null,
    ulong? AssignedToUserId = null,
    ulong? BranchId = null,
    // Ẩn bản Nháp của ma trận gắn nhiệm vụ (Tổ trưởng đang soạn) khỏi người xem là PHT.
    bool HideTaskDrafts = false,
    // Lọc từng chiều độc lập (không cần chọn đủ 3 chiều để ra một academicContextId).
    ulong? SubjectId = null,
    ulong? GradeLevelId = null);

public sealed record MatrixListRow(
    ulong Id,
    string Name,
    string Status,
    ulong? TaskId,
    ulong AcademicContextId,
    uint TotalQuestions,
    decimal TotalScore,
    ulong CreatedByUserId,
    ulong? ApprovedByUserId,
    string SubjectName,
    string GradeLevelName);

// A user as shown next to a matrix or task: name plus the role codes the school configured for them.
public sealed record MatrixPersonRow(ulong UserId, string FullName, IReadOnlyList<string> RoleCodes);

// The matrix created with a matrix task. Started = the task left ASSIGNED, or the matrix has details or left DRAFT.
public sealed record TaskMatrixRow(ulong Id, MatrixScope Scope, bool Started);

public sealed record MatrixExportInfo(
    string ContextLabel,
    IReadOnlyDictionary<ulong, string> LessonTitles);

public sealed record MatrixAcademicContextOption(
    ulong Id,
    string Label,
    ulong SchoolBranchId,
    ulong SubjectId,
    ulong GradeLevelId,
    // Display names so a client can show each dimension in its own field
    // instead of parsing them back out of Label.
    string SubjectName = "",
    string GradeLevelName = "");

// Lesson titles repeat across chapters (several "Luyện tập chung"), so the client labels and matches a
// lesson by its code and chapter too.
public sealed record MatrixLessonOption(
    ulong Id,
    ulong ContextId,
    ulong ChapterId,
    string Title,
    uint SortOrder,
    string Code = "",
    string ChapterCode = "",
    string ChapterTitle = "");

public sealed record MatrixTeamLeadOption(
    ulong Id,
    string Username,
    string FullName,
    ulong? SchoolBranchId);

public sealed record MatrixReferenceModel(
    IReadOnlyList<MatrixAcademicContextOption> AcademicContexts,
    IReadOnlyList<MatrixLessonOption> Lessons,
    IReadOnlyList<MatrixTeamLeadOption> TeamLeads);
