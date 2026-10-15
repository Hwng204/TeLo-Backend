namespace Domain.Entities.Examination;

// Bảng `test_result` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class TestResult
{
    public ulong Id { get; set; }
    public ulong ExamId { get; set; }
    public ulong ExamVariantId { get; set; }
    public ulong StudentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public decimal? TotalScore { get; set; }
    public string? Note { get; set; }
    public uint CorrectCount { get; set; }
    public uint WrongCount { get; set; }
    public Exam Exam { get; set; } = null!;
}
