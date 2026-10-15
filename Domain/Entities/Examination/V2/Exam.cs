using Domain.Entities.Academic;

namespace Domain.Entities.Examination;

// Bảng `exam` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class Exam
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public ulong SemesterId { get; set; }
    public ulong MatrixId { get; set; }
    public ulong SubjectId { get; set; }
    public ulong GradeLevelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime? ResultPublishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Semester Semester { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
