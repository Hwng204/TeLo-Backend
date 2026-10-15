using Application.Common;

namespace Application.DTOs;

public sealed record CreateMatrixTaskRequest(
    ulong AssignedToUserId,
    // MatrixScope.Key (phân hiệu, môn, khối); semesterId in the body is ignored.
    ulong AcademicContextId,
    DateTime? DueAt,
    string Name,
    string? Description);

public sealed record MatrixTaskResponse(
    ulong Id,
    ulong AssignedToUserId,
    ulong AcademicContextId,
    // Always null: tasks no longer have a semester.
    ulong? SemesterId,
    DateTime? DueAt,
    string Status,
    string TaskType,
    string? Description,
    ulong? MatrixId,
    string Name = "",
    MatrixPerson? CreatedBy = null)
{
    public string StatusLabel => Application.Common.MatrixTaskStatusCodes.Label(Status);
}

public sealed record MatrixTaskQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    ulong? AssignedToUserId = null,
    DateTime? DueBefore = null,
    ulong? BranchId = null,
    string? Keyword = null,
    ulong? AcademicContextId = null,
    // Lọc từng chiều độc lập. AcademicYearId và SemesterId bị bỏ qua: nhiệm vụ không còn năm học, học kỳ.
    ulong? AcademicYearId = null,
    ulong? SubjectId = null,
    ulong? GradeLevelId = null,
    ulong? SemesterId = null);

public sealed record MatrixTaskListItem(
    ulong Id,
    ulong CreatedByUserId,
    ulong AssignedToUserId,
    DateTime? DueAt,
    string Status,
    string TaskType,
    string? Description,
    ulong? AcademicContextId,
    // Always null: tasks no longer have a semester.
    ulong? SemesterId,
    ulong? MatrixId,
    string Name = "",
    MatrixPerson? CreatedBy = null)
{
    public string StatusLabel => Application.Common.MatrixTaskStatusCodes.Label(Status);
}

public sealed record MatrixTaskPage(
    IReadOnlyList<MatrixTaskListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
