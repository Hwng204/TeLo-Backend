using System.Linq.Expressions;
using Domain.Entities.QuestionBank;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;

namespace Application.Tests.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public IAcademicYearRepository AcademicYears =>
        throw new InvalidOperationException("Academic-year repository is not used by these tests.");
    public IProvinceRepository Provinces =>
        throw new InvalidOperationException("Province repository is not used by these tests.");
    public IMatrixRepository Matrices { get; set; } = new FakeMatrixRepository();
    public IMatrixTaskRepository MatrixTasks { get; set; } = new FakeMatrixTaskRepository();
    public IMatrixReferenceRepository MatrixReferences { get; set; } = new FakeMatrixReferenceRepository();
    public int ExecutionCount { get; private set; }

    public Task<int> CompleteAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        return await operation(cancellationToken);
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeMatrixRepository : IMatrixRepository
{
    public List<ExamMatrix> Items { get; } = new();
    public bool StatusUpdateResult { get; set; } = true;
    public bool LockResult { get; set; } = true;
    public List<(ulong TaskId, string Status)> TaskStatuses { get; } = new();
    public MatrixListFilter? LastListQuery { get; private set; }
    // Users the fake "database" knows about, for the author/approver lookups.
    public Dictionary<ulong, MatrixPersonRow> People { get; } = new();

    public Task<IReadOnlyDictionary<ulong, MatrixPersonRow>> GetPeopleAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyDictionary<ulong, MatrixPersonRow>>(
            People.Where(pair => userIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public Task<PagedResult<MatrixListRow>> ListAsync(
        MatrixListFilter filter,
        CancellationToken cancellationToken)
    {
        LastListQuery = filter;
        return Task.FromResult(new PagedResult<MatrixListRow>(
            Array.Empty<MatrixListRow>(),
            filter.Page,
            filter.PageSize,
            0));
    }

    public Task<ExamMatrix?> GetAsync(ulong id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
    }

    public Task<bool> ExistsForTaskAsync(ulong taskId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Any(item => item.TaskId == taskId));
    }

    public Task<bool> TryUpdateStatusAsync(
        ExamMatrix matrix,
        string expectedStatus,
        CancellationToken cancellationToken) => Task.FromResult(StatusUpdateResult);

    public Task<bool> LockWithStatusAsync(
        ulong matrixId,
        string expectedStatus,
        CancellationToken cancellationToken) => Task.FromResult(LockResult);

    public Task SetTaskStatusAsync(
        ulong taskId,
        string status,
        ulong updatedByUserId,
        CancellationToken cancellationToken)
    {
        TaskStatuses.Add((taskId, status));
        return Task.CompletedTask;
    }

    public Task<ExamMatrix?> GetByIdAsync(ulong id, CancellationToken cancellationToken = default) =>
        GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<ExamMatrix>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExamMatrix>>(Items.ToList());

    public Task<IReadOnlyList<ExamMatrix>> FindAsync(
        Expression<Func<ExamMatrix, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExamMatrix>>(Items.Where(predicate.Compile()).ToList());

    public Task AddAsync(ExamMatrix entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ExamMatrix entity) => Task.CompletedTask;

    public Task DeleteAsync(ExamMatrix entity)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMatrixTaskRepository : IMatrixTaskRepository
{
    public Dictionary<ulong, WorkTask> Tasks { get; } = new();
    public List<WorkTask> Items => Tasks.Values.ToList();
    public MatrixTaskFilter? LastQuery { get; private set; }

    public Task<PagedResult<MatrixTaskRow>> ListAsync(
        MatrixTaskFilter filter,
        CancellationToken cancellationToken)
    {
        LastQuery = filter;
        return Task.FromResult(new PagedResult<MatrixTaskRow>(
            Array.Empty<MatrixTaskRow>(),
            filter.Page,
            filter.PageSize,
            0));
    }

    public Task<WorkTask?> GetAsync(ulong taskId, CancellationToken cancellationToken)
    {
        Tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<WorkTask?> GetMatrixTaskAsync(ulong taskId, CancellationToken cancellationToken)
    {
        Tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task is { TaskType: "MATRIX" } ? task : null);
    }

    // taskId → matrixId of the matrix the Team Lead has saved for it.
    public Dictionary<ulong, ulong> LinkedMatrices { get; } = new();

    public Task<ulong?> GetLinkedMatrixIdAsync(ulong taskId, CancellationToken cancellationToken) =>
        Task.FromResult<ulong?>(LinkedMatrices.TryGetValue(taskId, out var matrixId) ? matrixId : null);

    public Task<bool> DeleteIfNotStartedAsync(ulong taskId, CancellationToken cancellationToken) =>
        Task.FromResult(!LinkedMatrices.ContainsKey(taskId) && Tasks.Remove(taskId));

    public Task<ulong?> GetContextBranchIdAsync(ulong academicContextId, CancellationToken cancellationToken) =>
        Task.FromResult<ulong?>(1);

    public Task<WorkTask?> GetByIdAsync(ulong id, CancellationToken cancellationToken = default) =>
        GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<WorkTask>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkTask>>(Items);

    public Task<IReadOnlyList<WorkTask>> FindAsync(
        Expression<Func<WorkTask, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkTask>>(Items.Where(predicate.Compile()).ToList());

    public Task AddAsync(WorkTask entity, CancellationToken cancellationToken = default)
    {
        entity.Id = (ulong)(Tasks.Count + 1);
        Tasks[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkTask entity) => Task.CompletedTask;

    public Task DeleteAsync(WorkTask entity)
    {
        Tasks.Remove(entity.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMatrixReferenceRepository : IMatrixReferenceRepository
{
    public ulong? LastRequiredBranchId { get; private set; }
    public int ValidationCount { get; private set; }

    public Task EnsureValidAsync(
        ulong academicContextId,
        ulong? semesterId,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken,
        ulong? requiredBranchId = null)
    {
        LastRequiredBranchId = requiredBranchId;
        return Task.CompletedTask;
    }

    public Task<MatrixExportInfo> GetExportInfoAsync(
        ulong academicContextId,
        ulong? semesterId,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MatrixExportInfo("ctx", null, new Dictionary<ulong, string>()));

    public Task EnsureAssignmentValidAsync(
        MatrixActor actor,
        ulong assignedToUserId,
        ulong academicContextId,
        ulong? semesterId,
        CancellationToken cancellationToken)
    {
        ValidationCount++;
        return Task.CompletedTask;
    }

    public Task<MatrixReferenceModel> GetReferenceDataAsync(
        MatrixActor actor,
        ulong? academicContextId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MatrixReferenceModel(
            Array.Empty<MatrixAcademicContextOption>(),
            Array.Empty<MatrixSemesterOption>(),
            Array.Empty<MatrixLessonOption>(),
            Array.Empty<MatrixTeamLeadOption>()));
}
