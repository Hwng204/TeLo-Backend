namespace Domain.Entities.Examination;

// Bảng `test_result_detail` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class TestResultDetail
{
    public ulong Id { get; set; }
    public ulong TestResultId { get; set; }
    public ulong QuestionId { get; set; }
    public string? SelectedOptionKey { get; set; }
    public decimal ScoreAwarded { get; set; }
}
