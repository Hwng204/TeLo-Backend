namespace Infrastructure.Exports;

public sealed record MatrixWorkbookRow(
    string LessonTitle,
    string CognitiveLevel,
    string QuestionType,
    uint QuestionCount,
    decimal Percentage,
    // Điểm ô suy ra = TotalScore * Percentage / 100, tính sẵn cho tiện xuất file.
    decimal CellScore);

public sealed record MatrixWorkbookModel(
    string Name,
    string Status,
    ulong? TaskId,
    string ContextLabel,
    string SemesterName,
    uint TotalQuestions,
    decimal TotalScore,
    IReadOnlyList<MatrixWorkbookRow> Rows);

public interface IMatrixWorkbookExporter
{
    byte[] Create(MatrixWorkbookModel matrix);
}
