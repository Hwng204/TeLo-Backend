using Domain.Entities.Identity;

namespace Domain.Entities.QuestionBank;

public sealed class WorkTask
{
    public ulong Id { get; set; }
    public ulong CreatedByUserId { get; set; }
    public ulong AssignedToUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = string.Empty;
    // Nullable vì `tasks` là bảng dùng chung (ExamSet, QuestionTask cũng tham chiếu); nhiệm vụ ma
    // trận bắt Name bắt buộc ở tầng Application (MatrixTaskApplicationService), không ở DB.
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ulong? UpdatedByUserId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public ulong? AcademicContextId { get; set; }
    public ulong? SemesterId { get; set; }
    // Null: người giao kiểm duyệt (mọi loại nhiệm vụ). Không còn gán mới, chỉ dữ liệu cũ có giá trị.
    public ulong? ReviewerUserId { get; set; }
    // Nhiệm vụ đề thi: ma trận đã duyệt dùng để tạo đề (kỳ thi chọn đề sau, khi tạo kỳ thi).
    public ulong? ExamMatrixId { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public User AssignedToUser { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
}
