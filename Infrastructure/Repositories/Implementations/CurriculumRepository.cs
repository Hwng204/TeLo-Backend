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

    public async Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken cancellationToken) =>
        await db.SubjectFields.AsNoTracking()
            .Where(field => field.Status == ActiveStatus && field.Subject.Status == ActiveStatus)
            .OrderBy(field => field.Subject.Name)
            .ThenBy(field => field.Id)
            .Select(field => new CurriculumFieldRow(field.Id, field.Name, field.SubjectId, field.Subject.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(
        ulong branchId,
        CancellationToken cancellationToken)
    {
        var chapters = await db.Chapters.AsNoTracking()
            .Where(chapter => chapter.SchoolBranchId == branchId)
            .Select(chapter => new
            {
                chapter.Id,
                chapter.GradeLevelId,
                GradeLevelName = chapter.GradeLevel.Name,
                chapter.FieldId,
                FieldName = chapter.Field.Name,
                chapter.Code,
                chapter.Title,
                chapter.SortOrder
            })
            .ToListAsync(cancellationToken);

        var lessons = await db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Chapter.SchoolBranchId == branchId)
            .Select(lesson => new CurriculumLessonRow(
                lesson.Id,
                lesson.ChapterId,
                lesson.Code,
                lesson.Title,
                lesson.SortOrder,
                db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                db.QuestionTasks.Any(task => task.LessonId == lesson.Id)))
            .ToListAsync(cancellationToken);

        var lessonsByChapter = lessons
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToLookup(lesson => lesson.ChapterId);

        return chapters
            .OrderBy(chapter => chapter.GradeLevelName, StringComparer.CurrentCulture)
            .ThenBy(chapter => chapter.SortOrder)
            .ThenBy(chapter => chapter.FieldId)
            .Select(chapter => new CurriculumChapterRow(
                chapter.Id,
                chapter.GradeLevelId,
                chapter.GradeLevelName,
                chapter.FieldId,
                chapter.FieldName,
                chapter.Code,
                chapter.Title,
                chapter.SortOrder,
                lessonsByChapter[chapter.Id].ToArray()))
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
    public async Task<(bool CodeTaken, bool TitleTaken)> FindChapterConflictAsync(
        ulong branchId,
        ulong gradeLevelId,
        ulong fieldId,
        string code,
        string title,
        ulong? exceptChapterId,
        CancellationToken cancellationToken)
    {
        var siblings = db.Chapters.AsNoTracking().Where(chapter =>
            chapter.SchoolBranchId == branchId &&
            chapter.GradeLevelId == gradeLevelId &&
            chapter.FieldId == fieldId &&
            (exceptChapterId == null || chapter.Id != exceptChapterId));
        return (
            await siblings.AnyAsync(chapter => chapter.Code == code, cancellationToken),
            await siblings.AnyAsync(chapter => chapter.Title == title, cancellationToken));
    }

    public async Task<(bool CodeTaken, bool TitleTaken)> FindLessonConflictAsync(
        ulong chapterId,
        string code,
        string title,
        ulong? exceptLessonId,
        CancellationToken cancellationToken)
    {
        var siblings = db.Lessons.AsNoTracking().Where(lesson =>
            lesson.ChapterId == chapterId &&
            (exceptLessonId == null || lesson.Id != exceptLessonId));
        return (
            await siblings.AnyAsync(lesson => lesson.Code == code, cancellationToken),
            await siblings.AnyAsync(lesson => lesson.Title == title, cancellationToken));
    }

    public Task<bool> IsChapterInUseAsync(ulong chapterId, CancellationToken cancellationToken) =>
        db.Lessons.AnyAsync(
            lesson => lesson.ChapterId == chapterId &&
                (db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                 db.QuestionTasks.Any(task => task.LessonId == lesson.Id)),
            cancellationToken);

    public async Task<bool> IsLessonInUseAsync(ulong lessonId, CancellationToken cancellationToken) =>
        await db.MatrixDetails.AnyAsync(detail => detail.LessonId == lessonId, cancellationToken) ||
        await db.QuestionTasks.AnyAsync(task => task.LessonId == lessonId, cancellationToken);

    public async Task<uint> NextChapterSortOrderAsync(
        ulong branchId,
        ulong gradeLevelId,
        CancellationToken cancellationToken) =>
        (await db.Chapters
            .Where(chapter => chapter.SchoolBranchId == branchId && chapter.GradeLevelId == gradeLevelId)
            .MaxAsync(chapter => (uint?)chapter.SortOrder, cancellationToken) ?? 0) + 1;

    public async Task<uint> NextLessonSortOrderAsync(ulong chapterId, CancellationToken cancellationToken) =>
        (await db.Lessons
            .Where(lesson => lesson.ChapterId == chapterId)
            .MaxAsync(lesson => (uint?)lesson.SortOrder, cancellationToken) ?? 0) + 1;

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
