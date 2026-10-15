using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class MatrixTaskRepository(ApplicationDbContext db)
    : GenericRepository<WorkTask>(db), IMatrixTaskRepository
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<MatrixTaskRow>> ListAsync(
        MatrixTaskFilter query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query);

        // matrix.task_id is unique: at most one matrix per task, which carries the task's scope.
        var tasks =
            from task in Db.WorkTasks.AsNoTracking().Where(task => task.TaskType == "MATRIX")
            join matrix in Db.ExamMatrices on (ulong?)task.Id equals matrix.TaskId into matrices
            from matrix in matrices.DefaultIfEmpty()
            select new { Task = task, Matrix = matrix };

        if (query.InvolvingUserId is { } userId)
            tasks = tasks.Where(row => row.Task.CreatedByUserId == userId || row.Task.AssignedToUserId == userId);

        if (query.AssignedToUserId is not null)
        {
            tasks = tasks.Where(row => row.Task.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.BranchId is not null)
        {
            tasks = tasks.Where(row => row.Matrix != null && row.Matrix.BranchId == query.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            tasks = tasks.Where(row => row.Task.Status == query.Status.Trim().ToUpperInvariant());
        }

        if (query.AcademicContextId is not null)
        {
            // A key that is not a scope matches nothing.
            var scope = MatrixScope.TryParse(query.AcademicContextId.Value, out var parsed) ? parsed : default;
            tasks = tasks.Where(row => row.Matrix != null &&
                row.Matrix.BranchId == scope.BranchId &&
                row.Matrix.SubjectId == scope.SubjectId &&
                row.Matrix.GradeLevelId == scope.GradeLevelId);
        }

        if (query.SubjectId is not null)
        {
            tasks = tasks.Where(row => row.Matrix != null && row.Matrix.SubjectId == query.SubjectId.Value);
        }

        if (query.GradeLevelId is not null)
        {
            tasks = tasks.Where(row => row.Matrix != null && row.Matrix.GradeLevelId == query.GradeLevelId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            // Gõ toàn số thì cũng khớp thẳng theo id, tiện khi biết id mà quên tên.
            var digits = new string(keyword.Where(char.IsDigit).ToArray());
            var byId = ulong.TryParse(digits, out var id) ? id : 0UL;
            tasks = tasks.Where(row =>
                EF.Functions.Like(row.Task.Name, $"%{keyword}%") ||
                (row.Task.Description != null && EF.Functions.Like(row.Task.Description, $"%{keyword}%")) ||
                (byId != 0UL && row.Task.Id == byId));
        }

        if (query.DueBefore is not null)
        {
            tasks = tasks.Where(row => row.Task.DueAt != null && row.Task.DueAt <= query.DueBefore);
        }

        var totalCount = await tasks.CountAsync(cancellationToken);
        var rows = await tasks
            .OrderByDescending(row => row.Task.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new
            {
                row.Task.Id,
                row.Task.CreatedByUserId,
                row.Task.AssignedToUserId,
                row.Task.DueAt,
                row.Task.Status,
                row.Task.TaskType,
                row.Task.Name,
                row.Task.Description,
                Matrix = row.Matrix == null
                    ? null
                    : new
                    {
                        row.Matrix.Id,
                        row.Matrix.BranchId,
                        row.Matrix.SubjectId,
                        row.Matrix.GradeLevelId,
                        Started = row.Task.Status != "ASSIGNED" || row.Matrix.Status != MatrixStatusCodes.Draft || row.Matrix.Details.Any()
                    }
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(task => new MatrixTaskRow(
                task.Id,
                task.CreatedByUserId,
                task.AssignedToUserId,
                task.DueAt,
                task.Status,
                task.TaskType,
                task.Name,
                task.Description,
                task.Matrix is null ? null : new MatrixScope(task.Matrix.BranchId, task.Matrix.SubjectId, task.Matrix.GradeLevelId).Key,
                task.Matrix is { Started: true } ? task.Matrix.Id : null))
            .ToArray();

        return new PagedResult<MatrixTaskRow>(items, query.Page, query.PageSize, totalCount);
    }

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

    // Checked and deleted under the task row lock: a team lead who has just filled the matrix (also under that
    // lock) makes this delete nothing, and a deleted task's matrix is deleted with it, so it cannot be filled later.
    public Task<bool> DeleteIfNotStartedAsync(
        ulong taskId,
        CancellationToken cancellationToken) =>
        Db.DeleteTaskIfNotStartedAsync(taskId, cancellationToken);

    private static void ValidatePage(MatrixTaskFilter query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        }

        if (query.PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        }
    }
}
