using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Examination;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class ExamServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesTrimmedDraftExam()
    {
        var repository = new FakeExamRepository();
        var service = new ExamService(repository);

        var result = await service.CreateAsync(
            ValidCreate() with { Name = "  Kiểm tra học kỳ  " },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kiểm tra học kỳ", result.Value?.Name);
        Assert.Equal(ExamStatusCodes.Draft, result.Value?.Status);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Theory]
    [InlineData(false, true, true, true, "SEMESTER_NOT_FOUND")]
    [InlineData(true, false, true, true, "SCHOOL_BRANCH_NOT_FOUND")]
    [InlineData(true, true, false, true, "GRADE_LEVEL_NOT_FOUND")]
    [InlineData(true, true, true, false, "SUBJECT_NOT_FOUND")]
    public async Task CreateAsync_RejectsMissingReference(
        bool semesterExists,
        bool branchExists,
        bool gradeLevelExists,
        bool subjectExists,
        string expectedCode)
    {
        var repository = new FakeExamRepository
        {
            References = new ExamReferenceData(
                semesterExists,
                branchExists,
                gradeLevelExists,
                subjectExists)
        };
        var service = new ExamService(repository);

        var result = await service.CreateAsync(ValidCreate(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal(0, repository.CompleteCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidDateRange()
    {
        var repository = new FakeExamRepository();
        var service = new ExamService(repository);
        var request = ValidCreate() with
        {
            StartDate = new DateOnly(2026, 12, 20),
            EndDate = new DateOnly(2026, 12, 10)
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("endDate", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task CreateAsync_RequiresGradeSubjectAndDateTimes()
    {
        var service = new ExamService(new FakeExamRepository());
        var request = ValidCreate() with
        {
            GradeLevelId = 0,
            SubjectId = 0,
            StartTime = null,
            EndDate = null
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("gradeLevelId", result.Error?.Details?.Keys ?? []);
        Assert.Contains("subjectId", result.Error?.Details?.Keys ?? []);
        Assert.Contains("startTime", result.Error?.Details?.Keys ?? []);
        Assert.Contains("endDate", result.Error?.Details?.Keys ?? []);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesOnlyExamScalarFields()
    {
        var repository = new FakeExamRepository();
        repository.Seed(new Exam
        {
            Id = 7,
            SemesterId = 1,
            SchoolBranchId = 2,
            GradeLevelId = 3,
            SubjectId = 4,
            Name = "Tên cũ",
            StartDate = new DateOnly(2026, 10, 1),
            StartTime = new TimeOnly(8, 0),
            EndDate = new DateOnly(2026, 10, 2),
            EndTime = new TimeOnly(10, 0),
            Status = ExamStatusCodes.Draft,
            Subjects = [new ExamSubject { Id = 99 }]
        });
        var service = new ExamService(repository);

        var result = await service.UpdateAsync(
            7,
            new UpdateExamRequest(
                1,
                2,
                3,
                4,
                "Tên mới",
                new DateOnly(2026, 11, 1),
                new TimeOnly(8, 0),
                new DateOnly(2026, 11, 2),
                new TimeOnly(10, 0),
                "scheduled"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Tên mới", result.Value?.Name);
        Assert.Equal(ExamStatusCodes.Scheduled, result.Value?.Status);
        Assert.Single(repository.ExamsById[7].Subjects);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_WhenExamDoesNotExist()
    {
        var service = new ExamService(new FakeExamRepository());

        var result = await service.UpdateAsync(99, ValidUpdate(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("EXAM_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidStatusBeforeWriting()
    {
        var repository = new FakeExamRepository();
        repository.Seed(ExistingExam());
        var service = new ExamService(repository);

        var result = await service.UpdateAsync(
            7,
            ValidUpdate() with { Status = "UNKNOWN" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Equal(0, repository.CompleteCallCount);
    }

    [Fact]
    public async Task UpdateAsync_RejectsMissingReferenceBeforeWriting()
    {
        var repository = new FakeExamRepository
        {
            References = new ExamReferenceData(true, true, false, true)
        };
        repository.Seed(ExistingExam());
        var service = new ExamService(repository);

        var result = await service.UpdateAsync(7, ValidUpdate(), CancellationToken.None);

        Assert.Equal("GRADE_LEVEL_NOT_FOUND", result.Error?.Code);
        Assert.Equal(0, repository.CompleteCallCount);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExamWithoutDependentData()
    {
        var repository = new FakeExamRepository();
        repository.Seed(ExistingExam());
        var service = new ExamService(repository);

        var result = await service.DeleteAsync(7, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain((ulong)7, repository.ExamsById.Keys);
        Assert.Equal(1, repository.CompleteCallCount);
    }

    [Fact]
    public async Task DeleteAsync_IsBlocked_WhenExamHasDependentData()
    {
        var repository = new FakeExamRepository { HasDependentData = true };
        repository.Seed(ExistingExam());
        var service = new ExamService(repository);

        var result = await service.DeleteAsync(7, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("EXAM_HAS_DEPENDENCIES", result.Error?.Code);
        Assert.Contains((ulong)7, repository.ExamsById.Keys);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_WhenExamDoesNotExist()
    {
        var result = await new ExamService(new FakeExamRepository())
            .DeleteAsync(99, CancellationToken.None);

        Assert.Equal("EXAM_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task ListAsync_NormalizesFiltersAndReturnsPagination()
    {
        var repository = new FakeExamRepository
        {
            ListItems =
            [
                new ExamListData(
                    7,
                    "Kiểm tra học kỳ",
                    1,
                    "Học kỳ 1",
                    5,
                    "2026-2027",
                    3,
                    "Khối 5",
                    4,
                    "Toán",
                    2,
                    "CS1",
                    "Cơ sở 1",
                    new DateOnly(2026, 11, 1),
                    new TimeOnly(8, 0),
                    new DateOnly(2026, 11, 2),
                    new TimeOnly(10, 0),
                    ExamStatusCodes.Draft)
            ],
            TotalCount = 21
        };
        var service = new ExamService(repository);

        var result = await service.ListAsync(
            new ExamListQuery(
                "  học kỳ ",
                1,
                2,
                "draft",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                2,
                10,
                "name",
                "asc"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(21, result.Value?.TotalCount);
        Assert.Equal(3, result.Value?.TotalPages);
        Assert.Equal("học kỳ", repository.LastFilter?.Keyword);
        Assert.Equal(ExamStatusCodes.Draft, repository.LastFilter?.Status);
        Assert.Equal(2, repository.LastFilter?.PageNumber);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSummaryCounts_AndNotFoundWhenMissing()
    {
        var repository = new FakeExamRepository
        {
            DetailCounts = (2, 3, 4, 5, 6)
        };
        repository.Seed(ExistingExam());
        var service = new ExamService(repository);

        var found = await service.GetByIdAsync(7, CancellationToken.None);
        var missing = await service.GetByIdAsync(8, CancellationToken.None);

        Assert.True(found.IsSuccess);
        Assert.Equal(2, found.Value?.SubjectCount);
        Assert.Equal(3, found.Value?.SessionCount);
        Assert.Equal(4, found.Value?.RoomCount);
        Assert.Equal(5, found.Value?.CandidateCount);
        Assert.Equal(6, found.Value?.ProctorCount);
        Assert.Equal("EXAM_NOT_FOUND", missing.Error?.Code);
    }

    [Fact]
    public async Task GetFormOptionsAsync_MapsAcademicYearsGradesAndSubjects()
    {
        var repository = new FakeExamRepository
        {
            FormOptions = new ExamFormOptionsData(
                [new ExamAcademicYearOptionData(1, "2026-2027", "ACTIVE", [new(2, "Học kỳ I")])],
                [new ExamOptionData(5, "Khối 5")],
                [new ExamOptionData(6, "Toán")])
        };
        var service = new ExamService(repository);

        var result = await service.GetFormOptionsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Học kỳ I", result.Value?.AcademicYears.Single().Semesters.Single().Name);
        Assert.Equal("Khối 5", result.Value?.GradeLevels.Single().Name);
        Assert.Equal("Toán", result.Value?.Subjects.Single().Name);
    }

    [Fact]
    public async Task GetCandidateCountAsync_ReturnsActiveRosterCount()
    {
        var service = new ExamService(new FakeExamRepository { CandidateCount = 100 });

        var result = await service.GetCandidateCountAsync(1, 2, 5, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value?.StudentCount);
        Assert.Equal((ulong)5, result.Value?.GradeLevelId);
    }

    [Fact]
    public async Task GetCandidateCountAsync_RejectsMissingScopeIds()
    {
        var service = new ExamService(new FakeExamRepository());

        var result = await service.GetCandidateCountAsync(0, 0, 0, CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.NotNull(result.Error?.Details);
        Assert.Equal(
            ["academicYearId", "gradeLevelId", "schoolBranchId"],
            result.Error.Details.Keys.OrderBy(key => key).ToArray());
    }

    [Fact]
    public async Task ListAsync_RejectsInvalidPagingBeforeRepositoryCall()
    {
        var repository = new FakeExamRepository();
        var service = new ExamService(repository);

        var result = await service.ListAsync(
            new ExamListQuery(null, null, null, null, null, null, 0, 101, "unknown", "sideways"),
            CancellationToken.None);

        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Null(repository.LastFilter);
    }

    private static CreateExamRequest ValidCreate() =>
        new(1, 2, 3, 4, "Kiểm tra học kỳ", new DateOnly(2026, 11, 1), new TimeOnly(8, 0), new DateOnly(2026, 11, 2), new TimeOnly(10, 0));

    private static UpdateExamRequest ValidUpdate() =>
        new(1, 2, 3, 4, "Kiểm tra học kỳ", new DateOnly(2026, 11, 1), new TimeOnly(8, 0), new DateOnly(2026, 11, 2), new TimeOnly(10, 0), ExamStatusCodes.Draft);

    private static Exam ExistingExam() =>
        new()
        {
            Id = 7,
            SemesterId = 1,
            SchoolBranchId = 2,
            GradeLevelId = 3,
            SubjectId = 4,
            Name = "Kiểm tra học kỳ",
            StartDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            EndDate = new DateOnly(2026, 11, 2),
            EndTime = new TimeOnly(10, 0),
            Status = ExamStatusCodes.Draft
        };

    private sealed class FakeExamRepository : IExamRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears => throw Unused();
        public IProvinceRepository Provinces => throw Unused();
        public IMatrixRepository Matrices => throw Unused();
        public IMatrixTaskRepository MatrixTasks => throw Unused();
        public IMatrixReferenceRepository MatrixReferences => throw Unused();
        public IExamRepository Exams => this;
        public IExamSubjectRepository ExamSubjects => throw Unused();
        public IExamRoomRepository ExamRooms => throw Unused();

        public Dictionary<ulong, Exam> ExamsById { get; } = [];
        public ExamReferenceData References { get; init; } = new(true, true);
        public bool HasDependentData { get; init; }
        public IReadOnlyList<ExamListData> ListItems { get; init; } = [];
        public int TotalCount { get; init; }
        public ExamListFilter? LastFilter { get; private set; }
        public int CompleteCallCount { get; private set; }
        public int CandidateCount { get; init; }
        public ExamFormOptionsData FormOptions { get; init; } = new([], [], []);
        public (int Subjects, int Sessions, int Rooms, int Candidates, int Proctors) DetailCounts { get; init; }

        public void Seed(Exam exam) => ExamsById[exam.Id] = exam;

        public Task<(IReadOnlyList<ExamListData> Items, int TotalCount)> ListAsync(
            ExamListFilter filter,
            CancellationToken cancellationToken)
        {
            LastFilter = filter;
            return Task.FromResult((ListItems, TotalCount));
        }

        public Task<ExamDetailData?> GetDetailAsync(ulong id, CancellationToken cancellationToken)
        {
            if (!ExamsById.TryGetValue(id, out var exam))
            {
                return Task.FromResult<ExamDetailData?>(null);
            }

            return Task.FromResult<ExamDetailData?>(new ExamDetailData(
                exam.Id,
                exam.Name,
                exam.SemesterId,
                "Học kỳ 1",
                5,
                "2026-2027",
                exam.GradeLevelId,
                "Khối 5",
                exam.SubjectId,
                "Toán",
                exam.SchoolBranchId,
                "CS1",
                "Cơ sở 1",
                exam.StartDate,
                exam.StartTime,
                exam.EndDate,
                exam.EndTime,
                exam.Status,
                DetailCounts.Subjects,
                DetailCounts.Sessions,
                DetailCounts.Rooms,
                DetailCounts.Candidates,
                DetailCounts.Proctors));
        }

        public Task<Exam?> GetByIdAsync(ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(ExamsById.GetValueOrDefault(id));

        public Task<Exam?> GetForDeleteAsync(ulong id, CancellationToken cancellationToken) =>
            GetByIdAsync(id, cancellationToken);

        public Task<ExamReferenceData> GetReferenceDataAsync(
            ulong semesterId,
            ulong schoolBranchId,
            ulong gradeLevelId,
            ulong subjectId,
            CancellationToken cancellationToken) => Task.FromResult(References);

        public Task<ExamFormOptionsData> GetFormOptionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(FormOptions);

        public Task<int> CountCandidatesAsync(
            ulong academicYearId,
            ulong schoolBranchId,
            ulong gradeLevelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CandidateCount);

        public Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken) =>
            Task.FromResult(HasDependentData);

        public Task AddAsync(Exam exam, CancellationToken cancellationToken)
        {
            exam.Id = 10;
            ExamsById[exam.Id] = exam;
            return Task.CompletedTask;
        }

        public void Delete(Exam exam) => ExamsById.Remove(exam.Id);

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

        private static InvalidOperationException Unused() =>
            new("Repository is not used by these tests.");
    }
}
