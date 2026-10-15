namespace Domain.Entities.Examination;

// Bảng `room_student` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class RoomStudent
{
    public ulong RoomId { get; set; }
    public ulong StudentId { get; set; }
}
