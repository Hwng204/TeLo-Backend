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
    public async Task ListAsync_ReturnsRoomsAndRejectsMissingExam()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var found = await new ExamRoomService(repository).ListAsync(10, CancellationToken.None);
        var missing = await new ExamRoomService(new FakeExamRoomRepository { ExamExists = false })
            .ListAsync(99, CancellationToken.None);

        Assert.Single(found.Value ?? []);
        Assert.Equal("READY", found.Value?.Single().Status);
        Assert.Equal("EXAM_NOT_FOUND", missing.Error?.Code);
    }

    [Fact]
    public async Task ListOptionsAsync_ReturnsActiveRoomsAndRejectsMissingExam()
    {
        var found = await new ExamRoomService(new FakeExamRoomRepository())
            .ListOptionsAsync(10, CancellationToken.None);
        var missing = await new ExamRoomService(new FakeExamRoomRepository { ExamExists = false })
            .ListOptionsAsync(99, CancellationToken.None);

        Assert.Equal("P101", found.Value?.Single().Code);
        Assert.Equal("EXAM_NOT_FOUND", missing.Error?.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetailAndNotFound()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var service = new ExamRoomService(repository);

        var found = await service.GetByIdAsync(10, 1, CancellationToken.None);
        var missing = await service.GetByIdAsync(10, 2, CancellationToken.None);

        Assert.Equal("PM-01", found.Value?.Code);
        Assert.Equal("EXAM_ROOM_NOT_FOUND", missing.Error?.Code);
    }

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
        Assert.Equal("K5P01", result.Value?.Code);
        Assert.Equal((uint)30, result.Value?.CandidateLimit);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsFailureWhenCreatedDetailCannotBeLoaded()
    {
        var result = await new ExamRoomService(new FakeExamRoomRepository { ReturnNullDetail = true })
            .CreateAsync(10, new CreateExamRoomRequest("PM-01", 20, 25), CancellationToken.None);

        Assert.Equal("EXAM_ROOM_NOT_FOUND", result.Error?.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("phong 01")]
    [InlineData("PM--01")]
    [InlineData("PM@01")]
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
    public async Task CreateAsync_RejectsMissingExam()
    {
        var result = await new ExamRoomService(new FakeExamRoomRepository { ExamExists = false })
            .CreateAsync(99, new CreateExamRoomRequest("PM-01", 20, 25), CancellationToken.None);

        Assert.Equal("EXAM_NOT_FOUND", result.Error?.Code);
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
    public async Task CreateAsync_RejectsDuplicatePhysicalRoom()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var service = new ExamRoomService(repository);

        var result = await service.CreateAsync(
            10,
            new CreateExamRoomRequest("PM-02", 20, 25),
            CancellationToken.None);

        Assert.Equal("EXAM_ROOM_ALREADY_EXISTS", result.Error?.Code);
    }

    [Fact]
    public async Task CreateBatchAsync_CreatesSelectedRoomsWithGeneratedCodes()
    {
        var repository = new FakeExamRoomRepository();
        var service = new ExamRoomService(repository);

        var result = await service.CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(2, 30, "pm-", [20, 21]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["PM-01", "PM-02"], result.Value?.Select(item => item.Code));
        Assert.All(result.Value ?? [], item => Assert.Equal((uint)30, item.CandidateLimit));
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Fact]
    public async Task CreateBatchAsync_SkipsCodesAlreadyUsedByExam()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 99, Code = "PM-01", CandidateLimit = 25 });
        var service = new ExamRoomService(repository);

        var result = await service.CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(2, 25, "PM-", [20, 21]),
            CancellationToken.None);

        Assert.Equal(["PM-02", "PM-03"], result.Value?.Select(room => room.Code));
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsRoomCountDifferentFromSelection()
    {
        var service = new ExamRoomService(new FakeExamRoomRepository());

        var result = await service.CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(2, 30, "PM-", [20]),
            CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("roomIds", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task CreateBatchAsync_RequiresExactlyTwoRooms()
    {
        var service = new ExamRoomService(new FakeExamRoomRepository());

        var result = await service.CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(1, 25, "PM-", [20]),
            CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("roomCount", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsMissingExamAndRoomFromAnotherBranch()
    {
        var missingExam = await new ExamRoomService(new FakeExamRoomRepository { ExamExists = false })
            .CreateBatchAsync(10, new CreateExamRoomsRequest(2, 25, "PM-", [20, 21]), CancellationToken.None);
        var invalidRoom = await new ExamRoomService(new FakeExamRoomRepository { RoomBelongsToExamBranch = false })
            .CreateBatchAsync(10, new CreateExamRoomsRequest(2, 25, "PM-", [20, 21]), CancellationToken.None);

        Assert.Equal("EXAM_NOT_FOUND", missingExam.Error?.Code);
        Assert.Equal("ROOM_NOT_FOUND", invalidRoom.Error?.Code);
    }

    [Fact]
    public async Task CreateBatchAsync_RejectsPhysicalRoomAlreadyConfigured()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "OLD-01", CandidateLimit = 25 });
        var result = await new ExamRoomService(repository).CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(2, 25, "PM-", [20, 21]),
            CancellationToken.None);

        Assert.Equal("EXAM_ROOM_ALREADY_EXISTS", result.Error?.Code);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsFailureWhenCreatedDetailCannotBeLoaded()
    {
        var repository = new FakeExamRoomRepository { ReturnNullDetail = true };

        var result = await new ExamRoomService(repository).CreateBatchAsync(
            10,
            new CreateExamRoomsRequest(2, 25, "PM-", [20, 21]),
            CancellationToken.None);

        Assert.Equal("EXAM_ROOM_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task GetPlanAsync_SplitsAlphabeticalRosterAcrossTwoRoomsAndSessions()
    {
        var students = Enumerable.Range(1, 100)
            .Select(index => new ExamCandidateData(
                (ulong)index,
                $"HS{index:000}",
                $"Học sinh {index:000}"))
            .ToArray();
        var repository = new FakeExamRoomRepository
        {
            CandidateRoster = new ExamCandidateRosterData(true, students)
        };
        var service = new ExamRoomService(repository);

        var result = await service.GetPlanAsync(10, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value?.TotalStudents);
        Assert.Equal(50, result.Value?.CapacityPerSession);
        Assert.Equal(2, result.Value?.RequiredSessionCount);
        Assert.Equal("HS001", result.Value?.Sessions[0].Rooms[0].Students[0].Code);
        Assert.Equal("HS025", result.Value?.Sessions[0].Rooms[0].Students[^1].Code);
        Assert.Equal("HS026", result.Value?.Sessions[0].Rooms[1].Students[0].Code);
        Assert.Equal("HS051", result.Value?.Sessions[1].Rooms[0].Students[0].Code);
        Assert.Equal("HS100", result.Value?.Sessions[1].Rooms[1].Students[^1].Code);
    }

    [Fact]
    public async Task GetPlanAsync_HandlesPartialFinalSession()
    {
        var students = Enumerable.Range(1, 51)
            .Select(index => new ExamCandidateData((ulong)index, $"HS{index:000}", $"Học sinh {index:000}"))
            .ToArray();
        var service = new ExamRoomService(new FakeExamRoomRepository
        {
            CandidateRoster = new ExamCandidateRosterData(true, students)
        });

        var result = await service.GetPlanAsync(10, 25, CancellationToken.None);

        Assert.Equal(2, result.Value?.RequiredSessionCount);
        Assert.Single(result.Value?.Sessions[1].Rooms[0].Students ?? []);
        Assert.Empty(result.Value?.Sessions[1].Rooms[1].Students ?? []);
    }

    [Fact]
    public async Task GetPlanAsync_ReturnsZeroSessionsForEmptyRoster()
    {
        var result = await new ExamRoomService(new FakeExamRoomRepository())
            .GetPlanAsync(10, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value?.TotalStudents);
        Assert.Empty(result.Value?.Sessions ?? []);
    }

    [Fact]
    public async Task GetPlanAsync_RejectsInvalidCapacityMissingExamAndMissingGrade()
    {
        var service = new ExamRoomService(new FakeExamRoomRepository());
        var invalidCapacity = await service.GetPlanAsync(10, 0, CancellationToken.None);
        var missingExam = await new ExamRoomService(new FakeExamRoomRepository { CandidateRoster = null })
            .GetPlanAsync(99, 25, CancellationToken.None);
        var missingGrade = await new ExamRoomService(new FakeExamRoomRepository
        {
            CandidateRoster = new ExamCandidateRosterData(false, [])
        }).GetPlanAsync(10, 25, CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", invalidCapacity.Error?.Code);
        Assert.Equal("EXAM_NOT_FOUND", missingExam.Error?.Code);
        Assert.Equal("EXAM_GRADE_NOT_CONFIGURED", missingGrade.Error?.Code);
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
    public async Task UpdateAsync_UpdatesRoomAndNormalizesCode()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var service = new ExamRoomService(repository);

        var result = await service.UpdateAsync(
            10,
            1,
            new UpdateExamRoomRequest(" pm-02 ", 21, 30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PM-02", result.Value?.Code);
        Assert.Equal((ulong)21, result.Value?.Room.Id);
        Assert.Equal((uint)30, result.Value?.CandidateLimit);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidPayloadWrongBranchAndDuplicate()
    {
        var invalid = await new ExamRoomService(new FakeExamRoomRepository())
            .UpdateAsync(10, 1, new UpdateExamRoomRequest("", 0, 0), CancellationToken.None);

        var wrongBranchRepository = new FakeExamRoomRepository { RoomBelongsToExamBranch = false };
        wrongBranchRepository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var wrongBranch = await new ExamRoomService(wrongBranchRepository)
            .UpdateAsync(10, 1, new UpdateExamRoomRequest("PM-02", 21, 25), CancellationToken.None);

        var duplicateRepository = new FakeExamRoomRepository();
        duplicateRepository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        duplicateRepository.Seed(new ExamRoom { Id = 2, ExamId = 10, RoomId = 21, Code = "PM-02", CandidateLimit = 25 });
        var duplicate = await new ExamRoomService(duplicateRepository)
            .UpdateAsync(10, 1, new UpdateExamRoomRequest("PM-02", 22, 25), CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", invalid.Error?.Code);
        Assert.Equal("ROOM_NOT_FOUND", wrongBranch.Error?.Code);
        Assert.Equal("EXAM_ROOM_CODE_ALREADY_EXISTS", duplicate.Error?.Code);
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

    [Fact]
    public async Task DeleteAsync_DeletesRoomAndReturnsNotFoundForMissingRoom()
    {
        var repository = new FakeExamRoomRepository();
        repository.Seed(new ExamRoom { Id = 1, ExamId = 10, RoomId = 20, Code = "PM-01", CandidateLimit = 25 });
        var service = new ExamRoomService(repository);

        var deleted = await service.DeleteAsync(10, 1, CancellationToken.None);
        var missing = await service.DeleteAsync(10, 2, CancellationToken.None);

        Assert.True(deleted.IsSuccess);
        Assert.Empty(repository.Items);
        Assert.Equal("EXAM_ROOM_NOT_FOUND", missing.Error?.Code);
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
        public bool ReturnNullDetail { get; init; }
        public ExamCandidateRosterData? CandidateRoster { get; init; } =
            new(true, []);
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
            Task.FromResult(!ReturnNullDetail && Items.TryGetValue(id, out var item) && item.ExamId == examId ? ToData(item) : null);

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

        public Task<ExamCandidateRosterData?> GetCandidateRosterAsync(
            ulong examId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CandidateRoster);

        public Task AddAsync(ExamRoom examRoom, CancellationToken cancellationToken)
        {
            examRoom.Id = Items.Count == 0 ? 100ul : Items.Keys.Max() + 1;
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
