namespace Application.Common;

// Trạng thái chung của mọi loại nhiệm vụ (GB-53). Nhiệm vụ ma trận: lưu nháp → Đang thực hiện, nộp → Chờ kiểm duyệt,
// bị từ chối → Cần chỉnh sửa, được duyệt → Hoàn thành (MatrixApplicationService đổi cùng transaction với ma trận).
public static class MatrixTaskStatusCodes
{
    public const string Assigned = "ASSIGNED";
    public const string InProgress = "IN_PROGRESS";
    public const string PendingReview = "PENDING_REVIEW";
    public const string NeedsRevision = "NEEDS_REVISION";
    public const string Completed = "COMPLETED";

    public static readonly IReadOnlyList<string> All = [Assigned, InProgress, PendingReview, NeedsRevision, Completed];

    public static string Label(string? status) => status switch
    {
        Assigned => "Đã giao",
        InProgress => "Đang thực hiện",
        PendingReview => "Chờ kiểm duyệt",
        NeedsRevision => "Cần chỉnh sửa",
        Completed => "Hoàn thành",
        _ => status ?? string.Empty
    };
}

public static class TaskTypeCodes
{
    public const string Matrix = "MATRIX";
    public const string Exam = "EXAM";
    public const string Question = "QUESTION";

    public static readonly IReadOnlyList<string> All = [Matrix, Exam, Question];

    public static string Label(string? type) => type switch
    {
        Matrix => "Ma trận",
        Exam => "Đề thi",
        Question => "Câu hỏi",
        _ => type ?? string.Empty
    };
}
