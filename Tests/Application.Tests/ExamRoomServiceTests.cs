using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Examination;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class ExamRoomServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesRoomAndNormalizesCode()
    {
        var repository = new FakeExamRoomRepository();
        var service = new ExamRoomService(repository);

        var result = await service.CreateAsync(
            10,
            new CreateExamRoomRequest(" K5P01 ", 20, 30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("k5p01", result.Value?.Code);
        Assert.Equal((uint)30, result.Value?.CandidateLimit);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("phong-01")]
    [InlineData("k6p01")]
    [InlineData("k5p1")]
    public async Task CreateAsync_RejectsInvalidCode(string code)
    {
        var service = new ExamRoomService(new FakeExamRoomRepository());

        var result = await service.CreateAsync(
            10,
            new CreateExamRoomRequest(code, 20, 30),
            CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("code", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task CreateAsync_RejectsRoomFromAnotherBranch()
    {
        var service = new ExamRoomService(new FakeExamRoomRepository { RoomBelongsToExamBranch = false });

        var result = await service.CreateAsync(
            10,
            new CreateExamRoomRequest("k5p01", 20, 30),
            CancellationToken.None);

        Assert.Equal("ROOM_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCode()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "k5p01", CandidateLimit = 30 });
        var service = new ExamRoomService(repository);

        var result = await service.CreateAsync(
            10,
            new CreateExamRoomRequest("K5P01", 21, 30),
            CancellationToken.None);

        Assert.Equal("EXAM_ROOM_CODE_ALREADY_EXISTS", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotFindRoomFromAnotherExam()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 11, RoomId = 20, Code = "k5p01", CandidateLimit = 30 });
        var service = new ExamRoomService(repository);

        var result = await service.UpdateAsync(
            10,
            1,
            new UpdateExamRoomRequest("k5p02", 21, 35),
            CancellationToken.None);

        Assert.Equal("EXAM_ROOM_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteAsync_IsBlockedWhenRoomHasSession()
    {
        var repository = new FakeExamRoomRepository { HasDependentData = true };
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "k5p01", CandidateLimit = 30 });
        var service = new ExamRoomService(repository);

        var result = await service.DeleteAsync(10, 1, CancellationToken.None);

        Assert.Equal("EXAM_ROOM_HAS_DEPENDENCIES", result.Error?.Code);
        Assert.Single(repository.Items);
    }

    private sealed class FakeExamRoomRepository : IExamRoomRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears => throw Unused();
        public IProvinceRepository Provinces => throw Unused();
        public IMatrixRepository Matrices => throw Unused();
        public IMatrixTaskRepository MatrixTasks => throw Unused();
        public IMatrixReferenceRepository MatrixReferences => throw Unused();
        public IExamRepository Exams => throw Unused();
        public IExamSubjectRepository ExamSubjects => throw Unused();
        public IExamRoomRepository ExamRooms => this;

        public Dictionary<ulong, ExamRoom> Items { get; } = [];
        public bool ExamExists { get; init; } = true;
        public bool RoomBelongsToExamBranch { get; init; } = true;
        public bool HasDependentData { get; init; }
        public int CompleteCallCount { get; private set; }

        public void Seed(ExamRoom item) => Items[item.Id] = item;

        public Task<IReadOnlyList<ExamRoomData>> ListByExamAsync(
            ulong examId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExamRoomData>>(Items.Values
                .Where(item => item.ExamId == examId)
                .Select(ToData)
                .ToArray());

        public Task<IReadOnlyList<ExamRoomOptionData>> ListRoomOptionsAsync(
            ulong examId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExamRoomOptionData>>(
                [new(20, "P101", "Phòng 101", "CLASSROOM", "ACTIVE")]);

        public Task<ExamRoomData?> GetDetailAsync(
            ulong examId,
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.TryGetValue(id, out var item) && item.ExamId == examId ? ToData(item) : null);

        public Task<ExamRoom?> GetByIdAsync(
            ulong examId,
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.TryGetValue(id, out var item) && item.ExamId == examId ? item : null);

        public Task<ExamRoom?> GetForDeleteAsync(
            ulong examId,
            ulong id,
            CancellationToken cancellationToken) =>
            GetByIdAsync(examId, id, cancellationToken);

        public Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken) =>
            Task.FromResult(ExamExists);

        public Task<bool> RoomBelongsToExamBranchAsync(
            ulong examId,
            ulong roomId,
            CancellationToken cancellationToken) =>
            Task.FromResult(RoomBelongsToExamBranch);

        public Task<bool> CodeExistsAsync(
            ulong examId,
            string code,
            ulong? excludedId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.Values.Any(item => item.ExamId == examId &&
                string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase) &&
                (!excludedId.HasValue || item.Id != excludedId.Value)));

        public Task<bool> RoomExistsAsync(
            ulong examId,
            ulong roomId,
            ulong? excludedId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.Values.Any(item => item.ExamId == examId &&
                item.RoomId == roomId &&
                (!excludedId.HasValue || item.Id != excludedId.Value)));

        public Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(HasDependentData);

        public Task AddAsync(ExamRoom examRoom, CancellationToken cancellationToken)
        {
            examRoom.Id = 100;
            Items[examRoom.Id] = examRoom;
            return Task.CompletedTask;
        }

        public void Delete(ExamRoom examRoom) => Items.Remove(examRoom.Id);

        public Task<int> CompleteAsync(CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) => operation(cancellationToken);

        public void Dispose()
        {
        }

        private static ExamRoomData ToData(ExamRoom item) =>
            new(
                item.Id,
                item.ExamId,
                item.Code,
                item.RoomId,
                $"P{item.RoomId}",
                $"Phòng {item.RoomId}",
                "CLASSROOM",
                "ACTIVE",
                item.CandidateLimit,
                item.Sessions.Count);

        private static InvalidOperationException Unused() => new("Repository is not used by these tests.");
    }
}
