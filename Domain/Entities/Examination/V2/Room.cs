namespace Domain.Entities.Examination;

// Bảng `room` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class Room
{
    public ulong Id { get; set; }
    public ulong ExamSessionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public uint Capacity { get; set; }
    public string? AccessCode { get; set; }
    public string Status { get; set; } = string.Empty;
}
