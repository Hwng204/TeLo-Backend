using Infrastructure.Repositories.Interface;

namespace Infrastructure.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IAcademicYearRepository AcademicYears { get; }
    IProvinceRepository Provinces { get; }
    IMatrixRepository Matrices { get; }
    IMatrixTaskRepository MatrixTasks { get; }
    IMatrixReferenceRepository MatrixReferences { get; }
    IExamRepository Exams { get; }

    // Writes every change tracked by the repositories in one SaveChanges call.
    Task<int> CompleteAsync(CancellationToken cancellationToken = default);

    // Runs the operation inside one database transaction; rolls back if it throws.
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
