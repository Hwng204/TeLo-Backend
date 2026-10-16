using Domain.Entities.Academic;

namespace Infrastructure.Repositories.Interface;

public sealed record CurriculumGradeRow(ulong Id, string Name);

// A "field" is a subject (the API keeps its old name): Id = SubjectId, Name = SubjectName.
public sealed record CurriculumFieldRow(ulong Id, string Name);

// Only ACTIVE chapters and lessons are rows. InUse: a matrix, a question task or a question references the
// lesson; for a chapter, any of its lessons, archived ones included.
public sealed record CurriculumLessonRow(
    ulong Id,
    ulong ChapterId,
    uint Number,
    string Title,
    bool InUse);

public sealed record CurriculumChapterRow(
    ulong Id,
    ulong GradeLevelId,
    string GradeLevelName,
    ulong FieldId,
    string FieldName,
    uint Number,
    string Title,
    bool InUse,
    IReadOnlyList<CurriculumLessonRow> Lessons);

public enum CurriculumSaveStatus
{
    Saved,
    // A unique index rejected the row: another request took the same number or title meanwhile.
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

    // Tracked. Null when missing, archived or in another branch.
    Task<Chapter?> GetChapterAsync(ulong branchId, ulong chapterId, CancellationToken cancellationToken);

    // Tracked. Null when missing, archived (or its chapter is) or in another branch.
    Task<Lesson?> GetLessonAsync(ulong branchId, ulong lessonId, CancellationToken cancellationToken);

    // Whether another ACTIVE chapter of the same branch, grade and subject already has this number / title.
    Task<(bool Number, bool Title)> FindChapterConflictAsync(
        ulong branchId,
        ulong gradeLevelId,
        ulong subjectId,
        uint number,
        string title,
        ulong? exceptChapterId,
        CancellationToken cancellationToken);

    // Whether another ACTIVE lesson of the chapter already has this number / title.
    Task<(bool Number, bool Title)> FindLessonConflictAsync(
        ulong chapterId,
        uint number,
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
