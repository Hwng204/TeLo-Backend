using Domain.Entities.Examination;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class ExamSubjectRepository(ApplicationDbContext context) : IExamSubjectRepository
{
    public async Task<IReadOnlyList<ExamSubjectData>> ListByExamAsync(
        ulong examId,
        CancellationToken cancellationToken) =>
        await Project(context.ExamSubjects.AsNoTracking().Where(item => item.ExamId == examId))
            .OrderBy(item => item.SubjectName)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

    public Task<ExamSubjectData?> GetDetailAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        Project(context.ExamSubjects.AsNoTracking().Where(item => item.ExamId == examId && item.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ExamSubject?> GetByIdAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        context.ExamSubjects.FirstOrDefaultAsync(
            item => item.ExamId == examId && item.Id == id,
            cancellationToken);

    public async Task<ExamSubject?> GetForDeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken)
    {
        var matches = await context.ExamSubjects
            .FromSqlInterpolated($"SELECT * FROM exam_subjects WHERE id = {id} AND exam_id = {examId} FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        return matches.SingleOrDefault();
    }

    public Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken) =>
        context.Exams.AsNoTracking().AnyAsync(exam => exam.Id == examId, cancellationToken);

    public Task<bool> SubjectExistsAsync(ulong subjectId, CancellationToken cancellationToken) =>
        context.Subjects.AsNoTracking().AnyAsync(subject => subject.Id == subjectId, cancellationToken);

    public Task<bool> ExistsAsync(
        ulong examId,
        ulong subjectId,
        ulong? excludedId,
        CancellationToken cancellationToken) =>
        context.ExamSubjects.AsNoTracking().AnyAsync(
            item => item.ExamId == examId &&
                item.SubjectId == subjectId &&
                (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);

    public Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken) =>
        context.ExamSubjectGradeLevels.AsNoTracking()
            .AnyAsync(item => item.ExamSubjectId == id, cancellationToken);

    public Task AddAsync(ExamSubject examSubject, CancellationToken cancellationToken) =>
        context.ExamSubjects.AddAsync(examSubject, cancellationToken).AsTask();

    public void Delete(ExamSubject examSubject) => context.ExamSubjects.Remove(examSubject);

    private static IQueryable<ExamSubjectData> Project(IQueryable<ExamSubject> query) =>
        query.Select(item => new ExamSubjectData(
            item.Id,
            item.ExamId,
            item.SubjectId,
            item.Subject.Name,
            item.DurationMinutes,
            item.Status,
            item.ResultPublishedAt,
            item.ResultPublishedByUserId,
            item.GradeLevels.Count));
}
