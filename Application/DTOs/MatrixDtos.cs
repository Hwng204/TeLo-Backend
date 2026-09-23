namespace Application.DTOs;

public sealed record MatrixDetailRequest(
    ulong LessonId,
    string CognitiveLevel,
    uint QuestionCount,
    // Tỷ lệ % điểm của dòng này (0, 100], không phải điểm tuyệt đối.
    decimal Percentage);

public sealed record SaveMatrixRequest(
    string Name,
    ulong AcademicContextId,
    ulong? SemesterId,
    ulong? TaskId,
    // Số nguyên dương do người lập tự đặt.
    int TotalScore,
    IReadOnlyList<MatrixDetailRequest> Details);

public sealed record MatrixDetailResponse(
    ulong Id,
    ulong LessonId,
    string CognitiveLevel,
    string QuestionType,
    uint QuestionCount,
    decimal Percentage,
    // Điểm ô suy ra = MatrixResponse.TotalScore * Percentage / 100, tính sẵn để frontend khỏi làm tròn lệch.
    decimal CellScore);

/// <summary>A user shown next to a matrix or task, with the role label the school uses for them.</summary>
public sealed record MatrixPerson(ulong UserId, string FullName, string? RoleLabel);

public sealed record MatrixResponse(
    ulong Id,
    string Name,
    string Status,
    ulong? TaskId,
    ulong AcademicContextId,
    ulong? SemesterId,
    IReadOnlyList<MatrixDetailResponse> Details,
    uint TotalQuestions,
    decimal TotalScore,
    IReadOnlyList<string> AllowedActions,
    string? RejectComment = null,
    DateTime? RejectedAt = null,
    ulong? RejectedByUserId = null,
    MatrixPerson? CreatedBy = null,
    DateTime? CreatedAt = null,
    MatrixPerson? ApprovedBy = null,
    DateTime? ApprovedAt = null)
{
    public string StatusLabel => Domain.Entities.QuestionBank.MatrixStatusCodes.Label(Status);
}


public sealed record MatrixExportFile(string FileName, byte[] Content);

public sealed record RejectMatrixRequest(string? Comment);
