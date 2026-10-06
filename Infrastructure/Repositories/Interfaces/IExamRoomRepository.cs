using Domain.Entities.Examination;

namespace Infrastructure.Repositories.Interface;

public sealed record ExamRoomData(
    ulong Id,
    ulong ExamId,
    string Code,
    ulong RoomId,
    string RoomCode,
    string RoomName,
    string RoomType,
    string RoomStatus,
    uint CandidateLimit,
    int SessionCount);

public sealed record ExamRoomOptionData(
    ulong Id,
    string Code,
    string Name,
    string RoomType,
    string Status);

public interface IExamRoomRepository
{
    Task<IReadOnlyList<ExamRoomData>> ListByExamAsync(ulong examId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExamRoomOptionData>> ListRoomOptionsAsync(
        ulong examId,
        CancellationToken cancellationToken);
    Task<ExamRoomData?> GetDetailAsync(ulong examId, ulong id, CancellationToken cancellationToken);
    Task<ExamRoom?> GetByIdAsync(ulong examId, ulong id, CancellationToken cancellationToken);
    Task<ExamRoom?> GetForDeleteAsync(ulong examId, ulong id, CancellationToken cancellationToken);
    Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken);
    Task<bool> RoomBelongsToExamBranchAsync(ulong examId, ulong roomId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(ulong examId, string code, ulong? excludedId, CancellationToken cancellationToken);
    Task<bool> RoomExistsAsync(ulong examId, ulong roomId, ulong? excludedId, CancellationToken cancellationToken);
    Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken);
    Task AddAsync(ExamRoom examRoom, CancellationToken cancellationToken);
    void Delete(ExamRoom examRoom);
}
