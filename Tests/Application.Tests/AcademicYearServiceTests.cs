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
    public async Task UpdateAsync_AllowsTermOnlyChangesForLegacyOverlappingYear()
    {
        var repository = new FakeAcademicYearRepository { HasScheduleConflict = true };
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var terms = ValidTerms();
        terms[1] = terms[1] with { Name = "Updated second term" };

        var result = await service.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, year.StartDate, year.EndDate, year.Version, terms),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated second term", result.Value!.Semesters[1].Name);

        var changedSchedule = await service.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, year.StartDate.AddDays(-1), year.EndDate, year.Version, terms),
            CancellationToken.None);
        Assert.False(changedSchedule.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CONFLICT", changedSchedule.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_ChangesYearAndTermsAsOneValidSchedule()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var request = new UpdateAcademicYearRequest("2027-2028", new DateOnly(2027, 8, 15), new DateOnly(2028, 5, 31), year.Version,
            [new(1, "I", new DateOnly(2027, 8, 15), new DateOnly(2028, 1, 15)), new(2, "II", new DateOnly(2028, 1, 16), new DateOnly(2028, 5, 31))]);
        var result = await service.UpdateAsync(year.Id, request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("2027-2028", result.Value!.Name);
        Assert.Equal(new DateOnly(2028, 1, 16), result.Value.Semesters[1].StartDate);
    }

    [Fact]
    public async Task UpdateAsync_InvalidTermsLeaveYearAndTermsUnchanged()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var version = year.Version;
        var request = new UpdateAcademicYearRequest("2027-2028", new DateOnly(2027, 8, 15), new DateOnly(2028, 5, 31), version, ValidTerms());
        var result = await service.UpdateAsync(year.Id, request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
        Assert.Equal("2026-2027", year.Name);
        Assert.Equal(version, year.Version);
        Assert.Equal(new DateOnly(2026, 8, 15), year.Semesters.First().StartDate);
    }

    [Fact]
    public async Task ConfigureTermsAsync_CanEditOpenTermWhilePreservingClosedTerm()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        year.Activate();
        year.Semesters.First().Close();
        var closedVersion = year.Semesters.First().Version;
        var terms = ValidTerms();
        terms[1] = terms[1] with { Name = "Second term updated" };
        var result = await service.ConfigureTermsAsync(year.Id, new ConfigureTermsRequest(terms, year.Version), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(closedVersion, year.Semesters.First().Version);
        Assert.Equal("Second term updated", year.Semesters.Last().Name);
    }

    private static ConfigureTermItem[] ValidTerms() =>
    [new(1, "I", new DateOnly(2026, 8, 15), new DateOnly(2027, 1, 15)), new(2, "II", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 31))];

    [Fact]
    public async Task CreateAsync_PersistsProvidedYearAndTermDatesTogether()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest() with
        {
            Terms =
            [
                new ConfigureTermItem(1, "  Term I  ", new DateOnly(2026, 8, 15), new DateOnly(2027, 1, 15)),
                new ConfigureTermItem(2, "  Term II  ", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 31))
            ]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var year = Assert.Single(repository.AddedYears);
        Assert.Equal("DRAFT", year.Status);
        Assert.Collection(year.Semesters.OrderBy(term => term.Order),
            first =>
            {
                Assert.Equal("Term I", first.Name);
                Assert.Equal(new DateOnly(2026, 8, 15), first.StartDate);
                Assert.Equal(new DateOnly(2027, 1, 15), first.EndDate);
            },
            second =>
            {
                Assert.Equal("Term II", second.Name);
                Assert.Equal(new DateOnly(2027, 1, 16), second.StartDate);
                Assert.Equal(new DateOnly(2027, 5, 31), second.EndDate);
            });
    }

    [Fact]
    public async Task CreateAsync_DoesNotPersistYearWhenProvidedTermsOverlap()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest() with
        {
            Terms =
            [
                new ConfigureTermItem(1, "Term I", new DateOnly(2026, 8, 15), new DateOnly(2027, 1, 15)),
                new ConfigureTermItem(2, "Term II", new DateOnly(2027, 1, 15), new DateOnly(2027, 5, 31))
            ]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("terms[1].startDate", result.Error?.Details?.Keys ?? []);
        Assert.Empty(repository.AddedYears);
        Assert.Equal(0, repository.CreateCallCount);
    }

    [Fact]
    public async Task CreateAsync_DoesNotPersistYearWhenProvidedTermsAreEmpty()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);

        var result = await service.CreateAsync(ValidRequest() with { Terms = [] }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Empty(repository.AddedYears);
        Assert.Equal(0, repository.CreateCallCount);
    }

    [Fact]
    public async Task UpdateAsync_RejectsStaleVersionWithoutChangingYear()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 8, 15),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            Version = 7
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);
        var request = new UpdateAcademicYearRequest(
            "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 5, 31), Version: 6);

        var result = await service.UpdateAsync(1, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 8, 15), year.StartDate);
        Assert.Equal(7u, year.Version);
    }

    [Fact]
    public async Task ConfigureTermsAsync_RejectsStaleVersionWithoutChangingTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 8, 15),
            EndDate = new DateOnly(2027, 5, 31),
            Status = "DRAFT",
            Version = 7,
            Semesters =
            [
                new Semester { Id = 10, Order = 1, Name = "Original I", Status = "PLANNED" },
                new Semester { Id = 11, Order = 2, Name = "Original II", Status = "PLANNED" }
            ]
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Updated I", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15)),
            new ConfigureTermItem(2, "Updated II", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 31))
        ], Version: 6);

        var result = await service.ConfigureTermsAsync(1, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(7u, year.Version);
        Assert.Equal(new[] { "Original I", "Original II" }, year.Semesters.OrderBy(term => term.Order).Select(term => term.Name));
        Assert.All(year.Semesters, term => Assert.Null(term.StartDate));
    }

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
        Assert.Equal("2026-2027", result.Value.Code);
        var year = Assert.Single(repository.AddedYears);
        Assert.Equal("2026-2027", year.Code);
        Assert.Throws<InvalidOperationException>(() => year.AssignCode("01-2027-2028"));
        Assert.Collection(
            year.Semesters.OrderBy(term => term.Order),
            first =>
            {
                Assert.Equal((byte)1, first.Order);
                Assert.Equal("Học kỳ I", first.Name);
                Assert.Equal("PLANNED", first.Status);
                Assert.Null(first.StartDate);
                Assert.Null(first.EndDate);
            },
            second =>
            {
                Assert.Equal((byte)2, second.Order);
                Assert.Equal("Học kỳ II", second.Name);
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
            Status = "CLOSED"
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
            Status = "DRAFT"
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
            Status = "DRAFT"
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
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1", Status = "PLANNED", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 15) },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2", Status = "PLANNED", StartDate = new DateOnly(2027, 1, 16), EndDate = new DateOnly(2027, 5, 31) }
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
        new("2026-2027", new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

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
        public IExamRoomRepository ExamRooms =>
            throw new InvalidOperationException("Exam-room repository is not used by these tests.");

        public AcademicYearCreateOutcome CreateOutcome { get; init; } =
            AcademicYearCreateOutcome.Created;
        public int CreateCallCount { get; private set; }
        public bool HasScheduleConflict { get; init; }
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
            ulong currentId,
            string name,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(HasScheduleConflict);

        public Task<bool> HasActiveYearAsync(
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
