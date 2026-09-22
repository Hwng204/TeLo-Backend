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

        var tasks = Db.WorkTasks
            .AsNoTracking()
            .Where(task => task.TaskType == "MATRIX");

        if (query.AssignedToUserId is not null)
        {
            tasks = tasks.Where(task => task.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.BranchId is not null)
        {
            var branchId = query.BranchId.Value;
            tasks = tasks.Where(task => Db.AcademicContexts.Any(context =>
                context.Id == task.AcademicContextId &&
                context.SchoolBranchId == branchId));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            tasks = tasks.Where(task => task.Status == query.Status.Trim().ToUpperInvariant());
        }

        if (query.AcademicContextId is not null)
        {
            tasks = tasks.Where(task => task.AcademicContextId == query.AcademicContextId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            // Mã nhiệm vụ không có trong bảng (suy ra từ id), nên "9301" hay "NV-MT-9301" phải tìm theo id.
            var digits = new string(keyword.Where(char.IsDigit).ToArray());
            var byId = ulong.TryParse(digits, out var id) ? id : 0UL;
            tasks = tasks.Where(task =>
                (task.Description != null && EF.Functions.Like(task.Description, $"%{keyword}%")) ||
                (byId != 0UL && task.Id == byId));
        }

        if (query.DueBefore is not null)
        {
            tasks = tasks.Where(task => task.DueAt != null && task.DueAt <= query.DueBefore);
        }

        var totalCount = await tasks.CountAsync(cancellationToken);
        var rows = await tasks
            .OrderByDescending(task => task.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(task => new
            {
                task.Id,
                task.CreatedByUserId,
                task.AssignedToUserId,
                task.DueAt,
                task.Status,
                task.TaskType,
                task.Description,
                task.AcademicContextId,
                task.SemesterId,
                MatrixId = Db.ExamMatrices
                    .Where(matrix => matrix.TaskId == task.Id)
                    .Select(matrix => (ulong?)matrix.Id)
                    .FirstOrDefault()
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
                task.Description,
                task.AcademicContextId,
                task.SemesterId,
                task.MatrixId))
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

    public Task<ulong?> GetLinkedMatrixIdAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return Db.ExamMatrices
            .AsNoTracking()
            .Where(matrix => matrix.TaskId == taskId)
            .Select(matrix => (ulong?)matrix.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ulong?> GetContextBranchIdAsync(
        ulong academicContextId,
        CancellationToken cancellationToken)
    {
        return Db.AcademicContexts
            .AsNoTracking()
            .Where(context => context.Id == academicContextId)
            .Select(context => (ulong?)context.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);
    }

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
