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
    public async Task UpdateAsync_ChecksNameConflictOnlyWhenTheNameChanges()
    {
        var repository = new FakeAcademicYearRepository { HasScheduleConflict = true };
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var terms = ValidTerms();
        terms[1] = terms[1] with { Name = "Updated second term" };

        var result = await service.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest(year.Name, terms),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated second term", result.Value!.Semesters[1].Name);

        var renamed = await service.UpdateAsync(year.Id,
            new UpdateAcademicYearRequest("2027-2028", terms),
            CancellationToken.None);
        Assert.False(renamed.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CONFLICT", renamed.Error!.Code);
        Assert.Equal("2026-2027", year.Name);
    }

    [Fact]
    public async Task UpdateAsync_RenamesYearAndTermsTogether()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var request = new UpdateAcademicYearRequest("2027-2028", [new(1, "Kỳ 1"), new(2, "Kỳ 2")]);
        var result = await service.UpdateAsync(year.Id, request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("2027-2028", result.Value!.Name);
        Assert.Equal("2027-2028", result.Value.Code);
        Assert.Equal("Kỳ 2", result.Value.Semesters[1].Name);
    }

    [Fact]
    public async Task UpdateAsync_InvalidTermsLeaveYearAndTermsUnchanged()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        var request = new UpdateAcademicYearRequest("2027-2028", [new(1, "Kỳ"), new(2, "kỳ")]);
        var result = await service.UpdateAsync(year.Id, request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code);
        Assert.Equal("2026-2027", year.Name);
        Assert.Equal(new[] { "I", "II" }, year.Semesters.OrderBy(term => term.Order).Select(term => term.Name));
    }

    [Fact]
    public async Task ConfigureTermsAsync_RenamesTermsOfAnActiveYear()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        await service.CreateAsync(ValidRequest() with { Terms = ValidTerms() }, CancellationToken.None);
        var year = Assert.Single(repository.AddedYears);
        year.Activate();
        var terms = ValidTerms();
        terms[1] = terms[1] with { Name = "Second term updated" };
        var result = await service.ConfigureTermsAsync(year.Id, new ConfigureTermsRequest(terms), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("I", year.Semesters.First().Name);
        Assert.Equal("Second term updated", year.Semesters.Last().Name);
    }

    private static ConfigureTermItem[] ValidTerms() => [new(1, "I"), new(2, "II")];

    [Fact]
    public async Task CreateAsync_PersistsProvidedTermNames()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest() with
        {
            Terms =
            [
                new ConfigureTermItem(1, "  Term I  "),
                new ConfigureTermItem(2, "  Term II  ")
            ]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var year = Assert.Single(repository.AddedYears);
        Assert.Equal("DRAFT", year.Status);
        Assert.Equal(new[] { "Term I", "Term II" }, year.Semesters.OrderBy(term => term.Order).Select(term => term.Name));
    }

    [Fact]
    public async Task CreateAsync_DoesNotPersistYearWhenProvidedTermNamesRepeat()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest() with
        {
            Terms =
            [
                new ConfigureTermItem(1, "Term"),
                new ConfigureTermItem(2, "term")
            ]
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.Error?.Code);
        Assert.Contains("terms[1].name", result.Error?.Details?.Keys ?? []);
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
    public async Task CreateAsync_CreatesDraftYearWithExactlyTwoDefaultTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var service = new AcademicYearService(repository);
        var request = ValidRequest();

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("DRAFT", result.Value.Status);
        Assert.Equal("2026-2027", result.Value.Code);
        Assert.Equal(2, result.Value.SemesterCount);
        var year = Assert.Single(repository.AddedYears);
        Assert.Collection(
            year.Semesters.OrderBy(term => term.Order),
            first =>
            {
                Assert.Equal((byte)1, first.Order);
                Assert.Equal("Học kỳ I", first.Name);
            },
            second =>
            {
                Assert.Equal((byte)2, second.Order);
                Assert.Equal("Học kỳ II", second.Name);
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
    public async Task CreateAsync_RejectsADuplicateYear()
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
            Status = "CLOSED"
        };
        repository.AddedYears.Add(closedYear);
        var service = new AcademicYearService(repository);

        var result = await service.UpdateAsync(
            1,
            new UpdateAcademicYearRequest("2025-2026"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ACADEMIC_YEAR_CLOSED", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName_WhenValid()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            Status = "DRAFT"
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.UpdateAsync(
            1,
            new UpdateAcademicYearRequest(" 2027-2028 "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2027-2028", result.Value?.Name);
        Assert.Equal("2027-2028", year.Name);
    }

    [Fact]
    public async Task ActivateAsync_ReturnsConflict_WhenIncompleteTerms()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
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
        var year = ConfiguredYear(1, 2026);
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.ActivateAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ACTIVE", result.Value?.Status);
    }

    [Fact]
    public async Task CloseAsync_ClosesActiveYear()
    {
        var repository = new FakeAcademicYearRepository();
        var year = ConfiguredYear(1, 2026);
        year.Activate();
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var result = await service.CloseAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CLOSED", result.Value?.Status);
        Assert.Equal(2, result.Value!.Semesters.Count);
    }

    [Fact]
    public async Task ActivateAsync_ReplacesCurrentYear()
    {
        var repository = new FakeAcademicYearRepository();
        var previous = ConfiguredYear(1, 2025);
        previous.Activate();
        var next = ConfiguredYear(2, 2026);
        repository.AddedYears.AddRange([previous, next]);
        var service = new AcademicYearService(repository);

        var result = await service.ActivateAsync(next.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CLOSED", previous.Status);
        Assert.Equal("ACTIVE", next.Status);
        Assert.Single(repository.AddedYears, year => year.Status == "ACTIVE");
    }

    [Fact]
    public async Task ActivateAsync_InvalidNewYearLeavesCurrentYearActive()
    {
        var repository = new FakeAcademicYearRepository();
        var previous = ConfiguredYear(1, 2025);
        previous.Activate();
        var next = ConfiguredYear(2, 2026);
        next.Semesters.Clear();
        repository.AddedYears.AddRange([previous, next]);

        var result = await new AcademicYearService(repository).ActivateAsync(next.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INCOMPLETE_TERMS", result.Error?.Code);
        Assert.Equal("ACTIVE", previous.Status);
        Assert.Equal("DRAFT", next.Status);
    }

    private static AcademicYear ConfiguredYear(ulong id, int firstYear) => new()
    {
        Id = id, Name = $"{firstYear}-{firstYear + 1}",
        Semesters = [
            new() { Order = 1, Name = "Học kỳ I" },
            new() { Order = 2, Name = "Học kỳ II" }
        ]
    };

    [Fact]
    public async Task ActivateAsync_ReturnsConcurrencyConflictWhenSwitchCannotBeSaved()
    {
        var repository = new FakeAcademicYearRepository { ActivationSucceeds = false };
        var previous = ConfiguredYear(1, 2025);
        previous.Activate();
        var target = ConfiguredYear(2, 2026);
        repository.AddedYears.AddRange([previous, target]);

        var result = await new AcademicYearService(repository).ActivateAsync(target.Id, CancellationToken.None);

        Assert.Equal("CONCURRENCY_CONFLICT", result.Error?.Code);
        Assert.Equal("ACTIVE", previous.Status);
        Assert.Equal("DRAFT", target.Status);
    }

    [Fact]
    public async Task ConfigureTermsAsync_UpdatesTermsSuccessfully()
    {
        var repository = new FakeAcademicYearRepository();
        var year = new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            Status = "DRAFT",
            Semesters = new List<Semester>
            {
                new() { Id = 10, Order = 1, Name = "Học kỳ 1" },
                new() { Id = 11, Order = 2, Name = "Học kỳ 2" }
            }
        };
        repository.AddedYears.Add(year);
        var service = new AcademicYearService(repository);

        var request = new ConfigureTermsRequest(new List<ConfigureTermItem>
        {
            new(1, "Học kỳ I"),
            new(2, "Học kỳ II")
        });

        var result = await service.ConfigureTermsAsync(1, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Học kỳ I", result.Value?.Semesters[0].Name);
        Assert.Equal("Học kỳ II", result.Value?.Semesters[1].Name);
    }

    [Fact]
    public async Task CloseTermAsync_ReturnsTermStatusRemoved()
    {
        var repository = new FakeAcademicYearRepository();
        repository.AddedYears.Add(ConfiguredYear(1, 2026));
        var service = new AcademicYearService(repository);

        var result = await service.CloseTermAsync(1, 10, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TERM_STATUS_REMOVED", result.Error?.Code);
    }

    private static CreateAcademicYearRequest ValidRequest() => new("2026-2027");

    private sealed class FakeAcademicYearRepository : IAcademicYearRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears => this;
        public IMatrixRepository Matrices =>
            throw new InvalidOperationException("Matrix repository is not used by these tests.");
        public IMatrixTaskRepository MatrixTasks =>
            throw new InvalidOperationException("Matrix task repository is not used by these tests.");
        public IMatrixReferenceRepository MatrixReferences =>
            throw new InvalidOperationException("Matrix reference repository is not used by these tests.");

        public AcademicYearCreateOutcome CreateOutcome { get; init; } =
            AcademicYearCreateOutcome.Created;
        public int CreateCallCount { get; private set; }
        public bool HasScheduleConflict { get; init; }
        public bool ActivationSucceeds { get; init; } = true;
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

        public Task<bool> TryActivateAsync(AcademicYear year, CancellationToken cancellationToken)
        {
            if (!ActivationSucceeds) return Task.FromResult(false);
            year.EnsureCanActivate();
            foreach (var current in AddedYears.Where(item => item.Id != year.Id && item.Status == "ACTIVE")) current.Close();
            year.Activate();
            return Task.FromResult(true);
        }

        public Task<AcademicYear?> GetActiveYearAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AddedYears.FirstOrDefault(y => y.Status == "ACTIVE"));

        public Task<ulong?> GetActiveYearIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AddedYears.Where(y => y.Status == "ACTIVE").Select(y => (ulong?)y.Id).FirstOrDefault());

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
