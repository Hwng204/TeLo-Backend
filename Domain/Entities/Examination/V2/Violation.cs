namespace Domain.Entities.Examination;

// Bảng `violation` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class Violation
{
    public ulong Id { get; set; }
    public ulong TestResultId { get; set; }
    public ulong ViolationTypeId { get; set; }
    public string? Decision { get; set; }
    public string? Description { get; set; }
    public DateTime OccurredAt { get; set; }
}
