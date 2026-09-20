using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.External.Provinces;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class ProvinceCatalogServiceTests
{
    [Fact]
    public async Task ListAsync_PrioritizesProvincesWithSchoolsThenSortsByName()
    {
        var repository = new FakeProvinceRepository(
        [
            new ProvinceOption("79", "Thành phố Hồ Chí Minh", false, 0),
            new ProvinceOption("48", "Đà Nẵng", true, 3),
            new ProvinceOption("01", "Hà Nội", true, 1)
        ]);
        var service = new ProvinceCatalogService(repository);

        var result = await service.ListAsync(CancellationToken.None);

        Assert.Collection(
            result,
            province => Assert.Equal("48", province.Code),
            province => Assert.Equal("01", province.Code),
            province => Assert.Equal("79", province.Code));
    }

    private sealed class FakeProvinceRepository(IReadOnlyList<ProvinceOption> options)
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

        public Task<IReadOnlyList<ProvinceOptionRow>> ListActiveAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProvinceOptionRow>>(options
                .Select(option => new ProvinceOptionRow(
                    option.Code,
                    option.Name,
                    option.HasSchools,
                    option.ActiveSchoolCount))
                .ToArray());

        public Task<IReadOnlySet<string>> ListActiveCodesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task SynchronizeAsync(
            IReadOnlyList<ProvinceCatalogRecord> provinces,
            string source,
            DateTimeOffset synchronizedAt,
            CancellationToken cancellationToken) => Task.CompletedTask;

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
