namespace Domain.Entities.Examination;

// Bảng `exam_session` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class ExamSession
{
    public ulong Id { get; set; }
    public ulong ExamId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
