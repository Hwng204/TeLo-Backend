using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.External.Provinces;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

using ProvinceCatalogItem = Infrastructure.External.Provinces.ProvinceCatalogRecord;

namespace Application.Tests;

public sealed class ProvinceSyncServiceTests
{
    [Fact]
    public async Task SynchronizeAsync_PersistsOnlyAfterProviderPayloadIsAccepted()
    {
        var provinces = Enumerable.Range(1, 34)
            .Select(index => new ProvinceCatalogItem(
                index.ToString("00"),
                $"Tỉnh {index}",
                "Tỉnh"))
            .ToArray();
        var repository = new FakeRepository(new HashSet<string>());
        var service = new ProvinceSyncService(
            new FakeProvider(provinces),
            repository,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero)));

        var result = await service.SynchronizeAsync(CancellationToken.None);

        Assert.Equal("TEST_PROVIDER", result.Provider);
        Assert.Equal(34, result.ProvinceCount);
        Assert.Equal(provinces, repository.SynchronizedProvinces);
    }

    [Fact]
    public async Task SynchronizeAsync_RejectsSuspiciousRemovalWithoutChangingCatalog()
    {
        var existingCodes = Enumerable.Range(1, 40)
            .Select(index => index.ToString("00"))
            .ToHashSet(StringComparer.Ordinal);
        var incoming = Enumerable.Range(1, 20)
            .Select(index => new ProvinceCatalogItem(index.ToString("00"), $"Tỉnh {index}", "Tỉnh"))
            .ToArray();
        var repository = new FakeRepository(existingCodes);
        var service = new ProvinceSyncService(
            new FakeProvider(incoming),
            repository,
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SynchronizeAsync(CancellationToken.None));

        Assert.Null(repository.SynchronizedProvinces);
    }

    private sealed class FakeProvider(IReadOnlyList<ProvinceCatalogItem> provinces)
        : IProvinceProvider
    {
        public string Name => "TEST_PROVIDER";

        public Task<IReadOnlyList<ProvinceCatalogItem>> FetchAsync(
            DateOnly asOfDate,
            CancellationToken cancellationToken) => Task.FromResult(provinces);
    }

    private sealed class FakeRepository(IReadOnlySet<string> activeCodes)
        : IProvinceRepository, IUnitOfWork
    {
        public IAcademicYearRepository AcademicYears =>
            throw new InvalidOperationException("Academic-year repository is not used by these tests.");
        public IProvinceRepository Provinces => this;
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

        public IReadOnlyList<ProvinceCatalogItem>? SynchronizedProvinces { get; private set; }

        public Task<IReadOnlyList<ProvinceOptionRow>> ListActiveAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProvinceOptionRow>>([]);

        public Task<IReadOnlySet<string>> ListActiveCodesAsync(
            CancellationToken cancellationToken) => Task.FromResult(activeCodes);

        public Task SynchronizeAsync(
            IReadOnlyList<ProvinceCatalogItem> provinces,
            string source,
            DateTimeOffset synchronizedAt,
            CancellationToken cancellationToken)
        {
            SynchronizedProvinces = provinces;
            return Task.CompletedTask;
        }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
