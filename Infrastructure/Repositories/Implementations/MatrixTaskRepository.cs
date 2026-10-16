using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class MatrixTaskRepository(ApplicationDbContext db)
    : GenericRepository<WorkTask>(db), IMatrixTaskRepository
{
    public Task<WorkTask?> GetAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return Db.WorkTasks
            .AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);
    }

    public Task LockAsync(ulong taskId, CancellationToken cancellationToken) =>
        Db.LockTaskAsync(taskId, cancellationToken);

    public Task<WorkTask?> GetMatrixTaskAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return Db.WorkTasks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                task => task.Id == taskId && task.TaskType == "MATRIX",
                cancellationToken);
    }

    public async Task<TaskMatrixRow?> GetTaskMatrixAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        var matrix = await Db.ExamMatrices
            .AsNoTracking()
            .Where(matrix => matrix.TaskId == taskId)
            .Select(matrix => new
            {
                matrix.Id,
                matrix.BranchId,
                matrix.SubjectId,
                matrix.GradeLevelId,
                Started = matrix.Task!.Status != "ASSIGNED" || matrix.Status != MatrixStatusCodes.Draft || matrix.Details.Any()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return matrix is null
            ? null
            : new TaskMatrixRow(matrix.Id, new MatrixScope(matrix.BranchId, matrix.SubjectId, matrix.GradeLevelId), matrix.Started);
    }
}
