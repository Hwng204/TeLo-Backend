using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Examination;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class ExamSubjectServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesActiveExamSubject()
    {
        var repository = new FakeExamSubjectRepository();
        var service = new ExamSubjectService(repository);

        var result = await service.CreateAsync(10, new CreateExamSubjectRequest(20, 90), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamSubjectStatusCodes.Active, result.Value?.Status);
        Assert.Equal((uint)90, result.Value?.DurationMinutes);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateSubject()
    {
        var repository = new FakeExamSubjectRepository();
        repository.Seed(new ExamSubject { Id = 1, ExamId = 10, SubjectId = 20, DurationMinutes = 60, Status = "ACTIVE" });
        var service = new ExamSubjectService(repository);

        var result = await service.CreateAsync(10, new CreateExamSubjectRequest(20, 90), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("EXAM_SUBJECT_ALREADY_EXISTS", result.Error?.Code);
        Assert.Equal(0, repository.CompleteCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsZeroDuration()
    {
        var service = new ExamSubjectService(new FakeExamSubjectRepository());

        var result = await service.CreateAsync(10, new CreateExamSubjectRequest(20, 0), CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("durationMinutes", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndNormalizesStatus()
    {
        var repository = new FakeExamSubjectRepository();
        repository.Seed(new ExamSubject { Id = 1, ExamId = 10, SubjectId = 20, DurationMinutes = 60, Status = "ACTIVE" });
        var service = new ExamSubjectService(repository);

        var result = await service.UpdateAsync(
            10,
            1,
            new UpdateExamSubjectRequest(21, 120, "inactive"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((ulong)21, result.Value?.Subject.Id);
        Assert.Equal((uint)120, result.Value?.DurationMinutes);
        Assert.Equal(ExamSubjectStatusCodes.Inactive, result.Value?.Status);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotFindSubjectFromAnotherExam()
    {
        var repository = new FakeExamSubjectRepository();
        repository.Seed(new ExamSubject { Id = 1, ExamId = 11, SubjectId = 20, DurationMinutes = 60, Status = "ACTIVE" });
        var service = new ExamSubjectService(repository);

        var result = await service.UpdateAsync(
            10,
            1,
            new UpdateExamSubjectRequest(20, 90, "ACTIVE"),
            CancellationToken.None);

        Assert.Equal("EXAM_SUBJECT_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteAsync_IsBlockedWhenGradeLevelExists()
    {
        var repository = new FakeExamSubjectRepository { HasDependentData = true };
        repository.Seed(new ExamSubject { Id = 1, ExamId = 10, SubjectId = 20, DurationMinutes = 60, Status = "ACTIVE" });
        var service = new ExamSubjectService(repository);

        var result = await service.DeleteAsync(10, 1, CancellationToken.None);

        Assert.Equal("EXAM_SUBJECT_HAS_DEPENDENCIES", result.Error?.Code);
        Assert.Single(repository.Items);
    }

    private sealed class FakeExamSubjectRepository : IExamSubjectRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears => throw Unused();
        public IProvinceRepository Provinces => throw Unused();
        public IMatrixRepository Matrices => throw Unused();
        public IMatrixTaskRepository MatrixTasks => throw Unused();
        public IMatrixReferenceRepository MatrixReferences => throw Unused();
        public IExamRepository Exams => throw Unused();
        public IExamSubjectRepository ExamSubjects => this;

        public Dictionary<ulong, ExamSubject> Items { get; } = [];
        public bool ExamExists { get; init; } = true;
        public bool SubjectExists { get; init; } = true;
        public bool HasDependentData { get; init; }
        public int CompleteCallCount { get; private set; }

        public void Seed(ExamSubject item) => Items[item.Id] = item;

        public Task<IReadOnlyList<ExamSubjectData>> ListByExamAsync(ulong examId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExamSubjectData>>(Items.Values
                .Where(item => item.ExamId == examId)
                .Select(ToData)
                .ToArray());

        public Task<ExamSubjectData?> GetDetailAsync(ulong examId, ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.TryGetValue(id, out var item) && item.ExamId == examId ? ToData(item) : null);

        public Task<ExamSubject?> GetByIdAsync(ulong examId, ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.TryGetValue(id, out var item) && item.ExamId == examId ? item : null);

        public Task<ExamSubject?> GetForDeleteAsync(ulong examId, ulong id, CancellationToken cancellationToken) =>
            GetByIdAsync(examId, id, cancellationToken);

        public Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken) =>
            Task.FromResult(ExamExists);

        public Task<bool> SubjectExistsAsync(ulong subjectId, CancellationToken cancellationToken) =>
            Task.FromResult(SubjectExists);

        public Task<bool> ExistsAsync(ulong examId, ulong subjectId, ulong? excludedId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Values.Any(item => item.ExamId == examId &&
                item.SubjectId == subjectId &&
                (!excludedId.HasValue || item.Id != excludedId.Value)));

        public Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(HasDependentData);

        public Task AddAsync(ExamSubject examSubject, CancellationToken cancellationToken)
        {
            examSubject.Id = 100;
            Items[examSubject.Id] = examSubject;
            return Task.CompletedTask;
        }

        public void Delete(ExamSubject examSubject) => Items.Remove(examSubject.Id);

        public Task<int> CompleteAsync(CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public void Dispose()
        {
        }

        private static ExamSubjectData ToData(ExamSubject item) =>
            new(item.Id, item.ExamId, item.SubjectId, $"Môn {item.SubjectId}", item.DurationMinutes,
                item.Status, item.ResultPublishedAt, item.ResultPublishedByUserId, item.GradeLevels.Count);

        private static InvalidOperationException Unused() => new("Repository is not used by these tests.");
    }
}
