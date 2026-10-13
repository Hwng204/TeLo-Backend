namespace Domain.Entities.QuestionBank;

// Một đề cần tạo trong nhiệm vụ đề thi (Đề 1 … Đề N theo số đề PHT chọn); đề đã soạn sẽ gắn vào dòng này.
public sealed class ExamTaskPaper
{
    public ulong Id { get; set; }
    public ulong TaskId { get; set; }
    public ulong? ExamSetId { get; set; }
    public ExamSet? ExamSet { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint SortOrder { get; set; }

    public WorkTask Task { get; set; } = null!;
}
