namespace Application.DTOs;

public sealed record MatrixDetailRequest(
    ulong LessonId,
    string CognitiveLevel,
    uint QuestionCount,
    // Tỷ lệ % điểm của dòng này (0, 100], không phải điểm tuyệt đối.
    decimal Percentage);

public sealed record SaveMatrixRequest(
    string Name,
    // MatrixScope.Key (phân hiệu, môn, khối); ignored for a task matrix, which takes the task's scope.
    ulong AcademicContextId,
    ulong? TaskId,
    // Số nguyên dương do người lập tự đặt, tối đa 999 (decimal(5,2)).
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
    decimal CellScore,
    // Tên/số bài và chương của dòng, có cả khi đã lưu trữ (INACTIVE) để ma trận cũ vẫn đọc được.
    string? LessonTitle = null,
    uint? LessonNumber = null,
    ulong? ChapterId = null,
    string? ChapterTitle = null,
    uint? ChapterNumber = null);

/// <summary>A user shown next to a matrix or task, with the role label the school uses for them.</summary>
public sealed record MatrixPerson(ulong UserId, string FullName, string? RoleLabel);

public sealed record MatrixResponse(
    ulong Id,
    string Name,
    string Status,
    ulong? TaskId,
    ulong AcademicContextId,
    IReadOnlyList<MatrixDetailResponse> Details,
    uint TotalQuestions,
    decimal TotalScore,
    IReadOnlyList<string> AllowedActions,
    string? RejectComment = null,
    DateTime? RejectedAt = null,
    ulong? RejectedByUserId = null,
    MatrixPerson? CreatedBy = null,
    MatrixPerson? ApprovedBy = null)
{
    public string StatusLabel => Domain.Entities.QuestionBank.MatrixStatusCodes.Label(Status);
}


public sealed record MatrixExportFile(string FileName, byte[] Content);

public sealed record RejectMatrixRequest(string? Comment);
