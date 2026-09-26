using Domain.Entities.Examination;

namespace Infrastructure.Repositories.Interface;

public sealed record ExamSubjectData(
    ulong Id,
    ulong ExamId,
    ulong SubjectId,
    string SubjectName,
    uint DurationMinutes,
    string Status,
    DateTime? ResultPublishedAt,
    ulong? ResultPublishedByUserId,
    int GradeLevelCount);

public interface IExamSubjectRepository
{
    Task<IReadOnlyList<ExamSubjectData>> ListByExamAsync(
        ulong examId,
        CancellationToken cancellationToken);

    Task<ExamSubjectData?> GetDetailAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);

    Task<ExamSubject?> GetByIdAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);

    Task<ExamSubject?> GetForDeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);

    Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken);
    Task<bool> SubjectExistsAsync(ulong subjectId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(ulong examId, ulong subjectId, ulong? excludedId, CancellationToken cancellationToken);
    Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken);
    Task AddAsync(ExamSubject examSubject, CancellationToken cancellationToken);
    void Delete(ExamSubject examSubject);
}
