namespace Application.DTOs;

public sealed record MatrixListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    ulong? AcademicContextId = null,
    ulong? SemesterId = null,
    string? Status = null,
    ulong? AssignedToUserId = null,
    ulong? BranchId = null,
    // Lọc từng chiều độc lập (không cần chọn đủ 4 chiều để ra một academicContextId).
    ulong? AcademicYearId = null,
    ulong? SubjectId = null,
    ulong? GradeLevelId = null);

public sealed record MatrixListItem(
    ulong Id,
    string Name,
    string Status,
    ulong? TaskId,
    ulong AcademicContextId,
    ulong? SemesterId,
    uint TotalQuestions,
    decimal TotalScore,
    MatrixPerson? CreatedBy = null,
    DateTime? CreatedAt = null,
    MatrixPerson? ApprovedBy = null,
    DateTime? ApprovedAt = null)
{
    public string StatusLabel => Domain.Entities.QuestionBank.MatrixStatusCodes.Label(Status);
}

public sealed record MatrixPage(
    IReadOnlyList<MatrixListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
