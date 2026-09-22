using Application.Common;

namespace Application.DTOs;

public sealed record CreateMatrixTaskRequest(
    ulong AssignedToUserId,
    ulong AcademicContextId,
    ulong? SemesterId,
    DateTime? DueAt,
    string? Description);

public sealed record MatrixTaskResponse(
    ulong Id,
    ulong AssignedToUserId,
    ulong AcademicContextId,
    ulong? SemesterId,
    DateTime? DueAt,
    string Status,
    string TaskType,
    string? Description,
    ulong? MatrixId,
    string Code = "",
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
    ulong? AcademicContextId = null);

public sealed record MatrixTaskListItem(
    ulong Id,
    ulong CreatedByUserId,
    ulong AssignedToUserId,
    DateTime? DueAt,
    string Status,
    string TaskType,
    string? Description,
    ulong? AcademicContextId,
    ulong? SemesterId,
    ulong? MatrixId,
    string Code = "",
    MatrixPerson? CreatedBy = null)
{
    public string StatusLabel => Application.Common.MatrixTaskStatusCodes.Label(Status);
}

public sealed record MatrixTaskPage(
    IReadOnlyList<MatrixTaskListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
