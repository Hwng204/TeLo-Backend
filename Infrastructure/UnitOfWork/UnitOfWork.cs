using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace Infrastructure.UnitOfWork;

public class UnitOfWork(
    ApplicationDbContext db,
    IAcademicYearRepository academicYears,
    IProvinceRepository provinces,
    IMatrixRepository matrices,
    IMatrixTaskRepository matrixTasks,
    IMatrixReferenceRepository matrixReferences,
    IExamRepository exams,
    IExamSubjectRepository examSubjects) : IUnitOfWork
{
    public IAcademicYearRepository AcademicYears { get; } = academicYears;
    public IProvinceRepository Provinces { get; } = provinces;
    public IMatrixRepository Matrices { get; } = matrices;
    public IMatrixTaskRepository MatrixTasks { get; } = matrixTasks;
    public IMatrixReferenceRepository MatrixReferences { get; } = matrixReferences;
    public IExamRepository Exams { get; } = exams;
    public IExamSubjectRepository ExamSubjects { get; } = examSubjects;

    public async Task<int> CompleteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            throw new MatrixDomainException(
                GetDuplicateCode(exception),
                "Ma trận xung đột với dữ liệu đã có (nhiệm vụ đã có ma trận hoặc dòng chi tiết bị trùng).");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackAsync(transaction);
            throw;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static async Task RollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the original operation exception.
        }
    }
    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        if (exception.GetBaseException() is not MySqlException { Number: 1062 } mysqlException)
        {
            return false;
        }

        return mysqlException.Message.Contains("uq_exam_matrices_task", StringComparison.OrdinalIgnoreCase) ||
            mysqlException.Message.Contains("uq_matrix_details_cell", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDuplicateCode(DbUpdateException exception)
    {
        var message = exception.GetBaseException().Message;
        return message.Contains("uq_exam_matrices_task", StringComparison.OrdinalIgnoreCase)
            ? "TaskAlreadyHasMatrix"
            : message.Contains("uq_matrix_details_cell", StringComparison.OrdinalIgnoreCase)
                ? "DuplicateDetail"
                : throw new InvalidOperationException("Unexpected non-matrix duplicate key.");
    }
}
