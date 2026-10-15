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

    // ACTIVE chapters with their ACTIVE lessons, ordered by number (id breaks ties). A chapter is in use when any of
    // its lessons is, archived ones included, so all lessons are read.
    public async Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(
        ulong branchId,
        CancellationToken cancellationToken)
    {
        var chapters = await db.Chapters.AsNoTracking()
            .Where(chapter => chapter.SchoolBranchId == branchId && chapter.Status == ActiveStatus)
            .Select(chapter => new
            {
                chapter.Id,
                chapter.GradeLevelId,
                GradeLevelName = chapter.GradeLevel.Name,
                chapter.SubjectId,
                SubjectName = chapter.Subject.Name,
                chapter.Number,
                chapter.Title
            })
            .ToListAsync(cancellationToken);

        var lessons = await db.Lessons.AsNoTracking()
            .Where(lesson => lesson.Chapter.SchoolBranchId == branchId && lesson.Chapter.Status == ActiveStatus)
            .Select(lesson => new
            {
                lesson.Id,
                lesson.ChapterId,
                lesson.Number,
                lesson.Title,
                // Compared in MySQL, like the active_flag column.
                Active = lesson.Status == ActiveStatus,
                InUse = db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                    db.QuestionTasks.Any(task => task.LessonId == lesson.Id) ||
                    db.Questions.Any(question => question.LessonId == lesson.Id)
            })
            .ToListAsync(cancellationToken);

        var lessonsByChapter = lessons.ToLookup(lesson => lesson.ChapterId);

        return chapters
            .Select(chapter => new CurriculumChapterRow(
                chapter.Id,
                chapter.GradeLevelId,
                chapter.GradeLevelName,
                chapter.SubjectId,
                chapter.SubjectName,
                chapter.Number,
                chapter.Title,
                lessonsByChapter[chapter.Id].Any(lesson => lesson.InUse),
                lessonsByChapter[chapter.Id]
                    .Where(lesson => lesson.Active)
                    .OrderBy(lesson => lesson.Number)
                    .ThenBy(lesson => lesson.Id)
                    .Select(lesson => new CurriculumLessonRow(
                        lesson.Id, lesson.ChapterId, lesson.Number, lesson.Title, lesson.InUse))
                    .ToArray()))
            .OrderBy(chapter => chapter.GradeLevelName, StringComparer.CurrentCulture)
            .ThenBy(chapter => chapter.FieldId)
            .ThenBy(chapter => chapter.Number)
            .ThenBy(chapter => chapter.Id)
            .ToArray();
    }

    public Task<Chapter?> GetChapterAsync(ulong branchId, ulong chapterId, CancellationToken cancellationToken) =>
        db.Chapters.SingleOrDefaultAsync(
                chapter => chapter.Id == chapterId &&
                    chapter.SchoolBranchId == branchId &&
                    chapter.Status == ActiveStatus,
                cancellationToken);

    public Task<Lesson?> GetLessonAsync(ulong branchId, ulong lessonId, CancellationToken cancellationToken) =>
        db.Lessons.SingleOrDefaultAsync(
                lesson => lesson.Id == lessonId &&
                    lesson.Status == ActiveStatus &&
                    lesson.Chapter.SchoolBranchId == branchId &&
                    lesson.Chapter.Status == ActiveStatus,
                cancellationToken);

    // Same scope as the unique indexes (ACTIVE rows only). Comparisons run in MySQL, so titles follow the column
    // collation.
    public async Task<(bool Number, bool Title)> FindChapterConflictAsync(
        ulong branchId,
        ulong gradeLevelId,
        ulong subjectId,
        uint number,
        string title,
        ulong? exceptChapterId,
        CancellationToken cancellationToken)
    {
        var siblings = db.Chapters.Where(chapter =>
            chapter.SchoolBranchId == branchId &&
            chapter.GradeLevelId == gradeLevelId &&
            chapter.SubjectId == subjectId &&
            chapter.Status == ActiveStatus &&
            (exceptChapterId == null || chapter.Id != exceptChapterId));
        return (await siblings.AnyAsync(chapter => chapter.Number == number, cancellationToken),
            await siblings.AnyAsync(chapter => chapter.Title == title, cancellationToken));
    }

    public async Task<(bool Number, bool Title)> FindLessonConflictAsync(
        ulong chapterId,
        uint number,
        string title,
        ulong? exceptLessonId,
        CancellationToken cancellationToken)
    {
        var siblings = db.Lessons.Where(lesson =>
            lesson.ChapterId == chapterId &&
            lesson.Status == ActiveStatus &&
            (exceptLessonId == null || lesson.Id != exceptLessonId));
        return (await siblings.AnyAsync(lesson => lesson.Number == number, cancellationToken),
            await siblings.AnyAsync(lesson => lesson.Title == title, cancellationToken));
    }

    public Task<bool> IsChapterInUseAsync(ulong chapterId, CancellationToken cancellationToken) =>
        db.Lessons.AnyAsync(
            lesson => lesson.ChapterId == chapterId &&
                (db.MatrixDetails.Any(detail => detail.LessonId == lesson.Id) ||
                 db.QuestionTasks.Any(task => task.LessonId == lesson.Id) ||
                 db.Questions.Any(question => question.LessonId == lesson.Id)),
            cancellationToken);

    public async Task<bool> IsLessonInUseAsync(ulong lessonId, CancellationToken cancellationToken) =>
        await db.MatrixDetails.AnyAsync(detail => detail.LessonId == lessonId, cancellationToken) ||
        await db.QuestionTasks.AnyAsync(task => task.LessonId == lessonId, cancellationToken) ||
        await db.Questions.AnyAsync(question => question.LessonId == lessonId, cancellationToken);

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
