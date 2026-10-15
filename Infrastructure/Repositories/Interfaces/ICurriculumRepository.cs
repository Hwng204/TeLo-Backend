using Domain.Entities.Academic;

namespace Infrastructure.Repositories.Interface;

public sealed record CurriculumGradeRow(ulong Id, string Name);

// A "field" is a subject (the API keeps its old name): Id = SubjectId, Name = SubjectName.
public sealed record CurriculumFieldRow(ulong Id, string Name, ulong SubjectId, string SubjectName);

// InUse: a matrix or a question task references the lesson (or, for a chapter, any of its lessons).
// Code is always "" (no storage); SortOrder is the 1-based position by id within the parent.
public sealed record CurriculumLessonRow(
    ulong Id,
    ulong ChapterId,
    string Code,
    string Title,
    uint SortOrder,
    bool InUse);

public sealed record CurriculumChapterRow(
    ulong Id,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong FieldId,
    string FieldName,
    string Code,
    string Title,
    uint SortOrder,
    IReadOnlyList<CurriculumLessonRow> Lessons)
{
    public bool InUse => Lessons.Any(lesson => lesson.InUse);
}

public enum CurriculumSaveStatus
{
    Saved,
    // A unique index rejected the row: another request created the same title meanwhile.
    Duplicate,
    // A foreign key blocked a delete: a matrix or task started using the row meanwhile.
    InUse
}

public interface ICurriculumRepository
{
    // Null when the user does not exist or has no branch.
    Task<ulong?> GetActorBranchIdAsync(ulong userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CurriculumGradeRow>> ListGradesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(
        ulong branchId,
        CancellationToken cancellationToken);

    // Tracked. Null when missing or in another branch.
    Task<Chapter?> GetChapterAsync(ulong branchId, ulong chapterId, CancellationToken cancellationToken);

    // Tracked. Null when missing or in another branch.
    Task<Lesson?> GetLessonAsync(ulong branchId, ulong lessonId, CancellationToken cancellationToken);

    Task<bool> IsChapterTitleTakenAsync(
        ulong branchId,
        ulong gradeLevelId,
        ulong subjectId,
        string title,
        ulong? exceptChapterId,
        CancellationToken cancellationToken);

    Task<bool> IsLessonTitleTakenAsync(
        ulong chapterId,
        string title,
        ulong? exceptLessonId,
        CancellationToken cancellationToken);

    Task<bool> IsChapterInUseAsync(ulong chapterId, CancellationToken cancellationToken);

    Task<bool> IsLessonInUseAsync(ulong lessonId, CancellationToken cancellationToken);

    void Add(Chapter chapter);

    void Add(Lesson lesson);

    void Remove(Chapter chapter);

    void Remove(Lesson lesson);

    Task<CurriculumSaveStatus> SaveAsync(CancellationToken cancellationToken);
}
