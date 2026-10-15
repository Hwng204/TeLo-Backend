namespace Domain.Entities.Examination;

// Bảng `exam_subject` của ERD v2: mới có cấu trúc, chủ module kỳ thi viết service sau.
public sealed class ExamSubject
{
    public ulong ExamId { get; set; }
    public ulong ExamSetId { get; set; }
    public bool IsPrimaryExam { get; set; }
}
