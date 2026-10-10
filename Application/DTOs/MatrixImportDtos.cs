namespace Application.DTOs;

public sealed record MatrixImportContext(ulong AcademicContextId, ulong? SemesterId, int TotalScore = 10, string? Name = null);
public sealed record MatrixImportError(int? RowNumber, string Message);
public sealed record MatrixImportPreview(
    bool CanImport,
    string? Name,
    int TotalScore,
    int LessonCount,
    int FilledLines,
    IReadOnlyList<MatrixDetailRequest> Details,
    IReadOnlyList<MatrixImportError> Errors);
