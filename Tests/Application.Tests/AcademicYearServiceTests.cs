using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Academic;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesDraftYearWithExactlyTwoPlannedTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest();

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DRAFT", result.Value.Status);
        Assert.Equal("01-2026-2027", result.Value.Code);
        var year = Assert.Single(repository.AddedYears);
        Assert.Equal("01", year.ProvinceCode);
        Assert.Equal("01-2026-2027", year.Code);
        Assert.Throws<InvalidOperationException>(() => year.AssignCode("01-2027-2028"));
        Assert.Collection(
            year.Semesters.OrderBy(term => term.Order),
            first =>
            {
                Assert.Equal((byte)1, first.Order);
                Assert.Equal("Học kỳ 1", first.Name);
                Assert.Equal("PLANNED", first.Status);
                Assert.Null(first.StartDate);
                Assert.Null(first.EndDate);
            },
            second =>
            {
                Assert.Equal((byte)2, second.Order);
                Assert.Equal("Học kỳ 2", second.Name);
                Assert.Equal("PLANNED", second.Status);
                Assert.Null(second.StartDate);
                Assert.Null(second.EndDate);
            });
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationDetailsBeforeUsingTheRepository()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest() with { Name = "2026/2027" };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("name", result.Error?.Details?.Keys ?? []);
        Assert.Equal(0, repository.CreateCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnUnknownProvince()
    {
        var repository = new FakeAcademicYearRepository
        {
            CreateOutcome = AcademicYearCreateOutcome.ProvinceNotFound
        };
        var service = new AcademicYearService(repository);

        var result = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROVINCE_NOT_FOUND", result.Error?.Code);
        Assert.Empty(repository.AddedYears);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnOverlappingOrDuplicateYear()
    {
        var repository = new FakeAcademicYearRepository
        {
            CreateOutcome = AcademicYearCreateOutcome.Conflict
        };
        var service = new AcademicYearService(repository);

        var result = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CONFLICT", result.Error?.Code);
        Assert.Empty(repository.AddedYears);
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflictWhenTheAtomicCreateLosesAConcurrentRace()
    {
        var repository = new FakeAcademicYearRepository
        {
            CreateOutcome = AcademicYearCreateOutcome.Conflict
        };
        var service = new AcademicYearService(repository);

        var result = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CONFLICT", result.Error?.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenYearDoesNotExist()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);

        var result = await service.GetByIdAsync(999, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsConflict_WhenYearIsClosed()
    {
        var repository = new FakeAcademicYearRepository();
        var closedYear = new AcademicYear
        {
            Id = 1,
            Name = "2025-2026",
            StartDate = new DateOnly(2025, 9, 1),
            EndDate = new DateOnly(2026, 5, 31),
            Status = "CLOSED",
            ProvinceCode = "01"
        };
        repository.AddedYears.Add(closedYear);
        var service = new AcademicYearService(repository);

        var result = await service.UpdateAsync(
            1,
            new UpdateAcademicYearRequest("2025-2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 1)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CLOSED", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDatesAndName_WhenValid()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            ProvinceCode = "01"
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.UpdateAsync(
            1,
            new UpdateAcademicYearRequest("2026-2027", new DateOnly(2026, 9, 5), new DateOnly(2027, 5, 25)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-2027", result.Value?.Name);
        Assert.Equal(new DateOnly(2026, 9, 5), result.Value?.StartDate);
        Assert.Equal(new DateOnly(2027, 5, 25), result.Value?.EndDate);
    }

    [Fact]
    public async Task ActivateAsync_ReturnsConflict_WhenIncompleteTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            ProvinceCode = "01"
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.ActivateAsync(1, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INCOMPLETE_TERMS", result.Error?.Code);
    }

    [Fact]
    public async Task ActivateAsync_ActivatesYear_WhenHasTwoTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            ProvinceCode = "01",
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1", Status = "PLANNED" },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2", Status = "PLANNED" }
            }
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.ActivateAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ACTIVE", result.Value?.Status);
    }

    [Fact]
    public async Task CloseAsync_ClosesYearAndCascadesToTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "ACTIVE",
            ProvinceCode = "01",
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1", Status = "ACTIVE" },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2", Status = "PLANNED" }
            }
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.CloseAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CLOSED", result.Value?.Status);
        Assert.All(result.Value!.Semesters, s => Assert.Equal("CLOSED", s.Status));
    }

    [Fact]
    public async Task ConfigureTermsAsync_UpdatesTermsSuccessfully()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            ProvinceCode = "01",
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1", Status = "PLANNED" },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2", Status = "PLANNED" }
            }
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var request = new ConfigureTermsRequest(new List<ConfigureTermItem>
        {
            new(1, "Học kỳ I", new DateOnly(2026, 9, 5), new DateOnly(2027, 1, 15)),
            new(2, "Học kỳ II", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 25))
        });

        var result = await service.ConfigureTermsAsync(1, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Học kỳ I", result.Value?.Semesters[0].Name);
        Assert.Equal("Học kỳ II", result.Value?.Semesters[1].Name);
    }

    [Fact]
    public async Task CloseTermAsync_ClosesSingleTerm()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "ACTIVE",
            ProvinceCode = "01",
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1", Status = "ACTIVE" },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2", Status = "PLANNED" }
            }
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.CloseTermAsync(1, 10, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CLOSED", result.Value?.Status);
    }

    private static CreateAcademicYearRequest ValidRequest() =>
        new("01", "2026-2027", new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

    private sealed class FakeAcademicYearRepository : IAcademicYearRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears => this;
        public IProvinceRepository Provinces =>
            throw new InvalidOperationException("Province repository is not used by these tests.");
        public IMatrixRepository Matrices =>
            throw new InvalidOperationException("Matrix repository is not used by these tests.");
        public IMatrixTaskRepository MatrixTasks =>
            throw new InvalidOperationException("Matrix task repository is not used by these tests.");
        public IMatrixReferenceRepository MatrixReferences =>
            throw new InvalidOperationException("Matrix reference repository is not used by these tests.");
        public IExamRepository Exams =>
            throw new InvalidOperationException("Exam repository is not used by these tests.");
        public IExamSubjectRepository ExamSubjects =>
            throw new InvalidOperationException("Exam-subject repository is not used by these tests.");

        public AcademicYearCreateOutcome CreateOutcome { get; init; } =
            AcademicYearCreateOutcome.Created;
        public int CreateCallCount { get; private set; }
        public List<AcademicYear> AddedYears { get; } = [];

        public Task<AcademicYearCreateOutcome> TryAddAsync(
            AcademicYear academicYear,
            CancellationToken cancellationToken)
        {
            CreateCallCount++;
            if (CreateOutcome == AcademicYearCreateOutcome.Created)
            {
                AddedYears.Add(academicYear);
                academicYear.Id = 10;
            }

            return Task.FromResult(CreateOutcome);
        }

        public Task<(IReadOnlyList<AcademicYear> Items, int TotalCount)> ListAsync(
            AcademicYearListFilter filter,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<AcademicYear>, int)>(([], 0));

        public Task<AcademicYear?> GetByIdWithSemestersAsync(
            ulong id,
            CancellationToken cancellationToken) =>
            Task.FromResult(AddedYears.FirstOrDefault(y => y.Id == id));

        public Task<bool> HasConflictExceptCurrentAsync(
            string provinceCode,
            ulong currentId,
            string name,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasActiveYearInProvinceAsync(
            string provinceCode,
            ulong currentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> UpdateAsync(
            AcademicYear academicYear,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<int> CompleteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public void Dispose()
        {
        }
    }
}
