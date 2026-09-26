using Domain.Entities.Academic;

namespace Domain.Entities.QuestionBank;

public sealed class MatrixDetail
{
    public ulong Id { get; set; }
    public ulong ExamMatrixId { get; set; }
    public ulong LessonId { get; set; }
    public string CognitiveLevel { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public uint QuestionCount { get; set; }
    // Tỷ lệ % điểm của dòng này trong tổng điểm ma trận (0, 100]. Điểm ô = ExamMatrix.TotalScore *
    // Percentage / 100 — suy ra, không lưu ở đây.
    public decimal Percentage { get; set; }

    public ExamMatrix ExamMatrix { get; set; } = null!;
    public TextbookLesson Lesson { get; set; } = null!;
}
