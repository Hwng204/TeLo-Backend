using Infrastructure.Repositories.Interface;

namespace Application.DTOs;

// Everything the chapter and lesson screen needs in one call: the branch is small enough
// (tens of chapters, a few hundred lessons) for the client to search and filter locally.
// The repository rows are returned as they are, like MatrixReferenceData does with its options.
public sealed record CurriculumDto(
    bool CanManage,
    IReadOnlyList<CurriculumGradeRow> Grades,
    IReadOnlyList<CurriculumFieldRow> Fields,
    IReadOnlyList<CurriculumChapterRow> Chapters);

public sealed record SaveChapterRequest(ulong GradeLevelId, ulong FieldId, string? Code, string? Title);

public sealed record SaveLessonRequest(string? Code, string? Title);

public sealed record CurriculumImportErrorDto(int RowNumber, string Message);

public sealed record CurriculumImportPreviewDto(
    int RowCount,
    int ChapterCount,
    int NewChapterCount,
    int LessonCount,
    IReadOnlyList<CurriculumImportErrorDto> Errors)
{
    public bool CanImport => Errors.Count == 0;
}

public sealed record CurriculumImportResultDto(int NewChapterCount, int LessonCount);

public sealed record CurriculumFile(string FileName, byte[] Content);

// Who is calling: the branch is looked up from the user, never taken from the request.
public sealed record CurriculumActor(ulong UserId, bool CanManage);
