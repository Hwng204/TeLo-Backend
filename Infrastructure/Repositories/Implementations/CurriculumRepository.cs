using Domain.Entities.Academic;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class CurriculumRepository(ApplicationDbContext db) : ICurriculumRepository
{
    private const string ActiveStatus = "ACTIVE";

    public Task<ulong?> GetActorBranchIdAsync(ulong userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.Status == ActiveStatus)
            .Select(user => user.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CurriculumGradeRow>> ListGradesAsync(CancellationToken cancellationToken) =>
        await db.GradeLevels.AsNoTracking()
            .Where(grade => grade.Status == ActiveStatus)
            .OrderBy(grade => grade.Name)
            .Select(grade => new CurriculumGradeRow(grade.Id, grade.Name))
            .ToListAsync(cancellationToken);

    // Fields are subjects now; the row keeps its shape so the API does not change.
    public async Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken cancellationToken) =>
        await db.Subjects.AsNoTracking()
            .Where(subject => subject.Status == ActiveStatus)
            .OrderBy(subject => subject.Name)
            .ThenBy(subject => subject.Id)
            .Select(subject => new CurriculumFieldRow(subject.Id, subject.Name, subject.Id, subject.Name))
            .ToListAsync(cancellationToken);

    // Code is no longer stored ("" in the rows); SortOrder is the position by id: chapters within their
    // grade and subject, lessons within their chapter.
    public async Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(
        ulong branchId,
        CancellationToken cancellationToken)
    {
        var chapters = await db.Chapters.AsNoTracking()
            .Where(chapter => chapter.SchoolBranchId == branchId)
            .OrderBy(chapter => chapter.Id)
            .Select(chapter => new
            {
                chapter.Id,
                chapter.GradeLevelId,
                GradeLevelName = chapter.GradeLevel.Name,
                chapter.SubjectId,
                SubjectName = chapter.Subject.Name,
                chapter.Title
            })
            .ToListAsync(cancellationToken);

        var lessons = await db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Chapter.SchoolBranchId == branchId)
            .OrderBy(lesson => lesson.Id)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.ChapterId,
                lesson.Title,
                InUse = db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                    db.QuestionTasks.Any(task => task.LessonId == lesson.Id)
            })
            .ToListAsync(cancellationToken);

        var lessonsByChapter = lessons.ToLookup(lesson => lesson.ChapterId);

        return chapters
            .GroupBy(chapter => (chapter.GradeLevelId, chapter.SubjectId))
            .SelectMany(group => group.Select((chapter, index) => new CurriculumChapterRow(
                chapter.Id,
                chapter.GradeLevelId,
                chapter.GradeLevelName,
                chapter.SubjectId,
                chapter.SubjectName,
                string.Empty,
                chapter.Title,
                (uint)index + 1,
                lessonsByChapter[chapter.Id]
                    .Select((lesson, position) => new CurriculumLessonRow(
                        lesson.Id, lesson.ChapterId, string.Empty, lesson.Title, (uint)position + 1, lesson.InUse))
                    .ToArray())))
            .OrderBy(chapter => chapter.GradeLevelName, StringComparer.CurrentCulture)
            .ThenBy(chapter => chapter.FieldId)
            .ThenBy(chapter => chapter.SortOrder)
            .ToArray();
    }

    public Task<Chapter?> GetChapterAsync(ulong branchId, ulong chapterId, CancellationToken cancellationToken) =>
        db.Chapters.SingleOrDefaultAsync(
                chapter => chapter.Id == chapterId && chapter.SchoolBranchId == branchId,
                cancellationToken);

    public Task<Lesson?> GetLessonAsync(ulong branchId, ulong lessonId, CancellationToken cancellationToken) =>
        db.Lessons.SingleOrDefaultAsync(
                lesson => lesson.Id == lessonId && lesson.Chapter.SchoolBranchId == branchId,
                cancellationToken);

    // Comparisons run in MySQL, so they follow the column collation: case-insensitive, accent-sensitive.
    public Task<bool> IsChapterTitleTakenAsync(
        ulong branchId,
        ulong gradeLevelId,
        ulong subjectId,
        string title,
        ulong? exceptChapterId,
        CancellationToken cancellationToken) =>
        db.Chapters.AnyAsync(chapter =>
                chapter.SchoolBranchId == branchId &&
                chapter.GradeLevelId == gradeLevelId &&
                chapter.SubjectId == subjectId &&
                chapter.Title == title &&
                (exceptChapterId == null || chapter.Id != exceptChapterId),
            cancellationToken);

    public Task<bool> IsLessonTitleTakenAsync(
        ulong chapterId,
        string title,
        ulong? exceptLessonId,
        CancellationToken cancellationToken) =>
        db.Lessons.AnyAsync(lesson =>
                lesson.ChapterId == chapterId &&
                lesson.Title == title &&
                (exceptLessonId == null || lesson.Id != exceptLessonId),
            cancellationToken);

    public Task<bool> IsChapterInUseAsync(ulong chapterId, CancellationToken cancellationToken) =>
        db.Lessons.AnyAsync(
            lesson => lesson.ChapterId == chapterId &&
                (db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                 db.QuestionTasks.Any(task => task.LessonId == lesson.Id)),
            cancellationToken);

    public async Task<bool> IsLessonInUseAsync(ulong lessonId, CancellationToken cancellationToken) =>
        await db.MatrixDetails.AnyAsync(detail => detail.LessonId == lessonId, cancellationToken) ||
        await db.QuestionTasks.AnyAsync(task => task.LessonId == lessonId, cancellationToken);

    public void Add(Chapter chapter) => db.Chapters.Add(chapter);

    public void Add(Lesson lesson) => db.Lessons.Add(lesson);

    public void Remove(Chapter chapter) => db.Chapters.Remove(chapter);

    public void Remove(Lesson lesson) => db.Lessons.Remove(lesson);

    public async Task<CurriculumSaveStatus> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CurriculumSaveStatus.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException
        {
            ErrorCode: MySqlErrorCode.DuplicateKeyEntry or
                MySqlErrorCode.RowIsReferenced2 or
                MySqlErrorCode.RowIsReferenced
        } mysql)
        {
            db.ChangeTracker.Clear();
            return mysql.ErrorCode == MySqlErrorCode.DuplicateKeyEntry
                ? CurriculumSaveStatus.Duplicate
                : CurriculumSaveStatus.InUse;
        }
    }
}
