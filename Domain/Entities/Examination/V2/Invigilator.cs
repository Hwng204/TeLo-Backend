namespace Domain.Entities.Examination;

// Bảng `invigilator` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class Invigilator
{
    public ulong TeacherId { get; set; }
    public ulong RoomId { get; set; }
    public string Position { get; set; } = string.Empty;
}
