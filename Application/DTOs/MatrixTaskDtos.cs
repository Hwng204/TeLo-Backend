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
