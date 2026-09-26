namespace Domain.Entities.QuestionBank;

public sealed record MatrixDetailValue(
    ulong LessonId,
    string CognitiveLevel,
    string QuestionType,
    uint QuestionCount,
    // Tỷ lệ % điểm của dòng này trong tổng điểm ma trận (0, 100]. Điểm ô/điểm mỗi câu là giá trị
    // suy ra (TotalScore * Percentage / 100), không lưu trực tiếp.
    decimal Percentage);

