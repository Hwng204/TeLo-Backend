using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class ExamMatrixRepository(ApplicationDbContext db)
    : GenericRepository<ExamMatrix>(db), IMatrixRepository
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<MatrixListRow>> ListAsync(
        MatrixListFilter query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query);

        var matrices = Db.ExamMatrices.AsNoTracking();

        if (string.IsNullOrWhiteSpace(query.Status))
        {
            matrices = matrices.Where(matrix => matrix.Status != MatrixStatusCodes.Archived);
        }
        else
        {
            var status = query.Status.Trim().ToUpperInvariant();
            if (!MatrixStatusCodes.IsKnown(status))
            {
                throw new ArgumentException("Trạng thái ma trận không hợp lệ.", nameof(query));
            }

            matrices = matrices.Where(matrix => matrix.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            matrices = matrices.Where(matrix =>
                EF.Functions.Like(matrix.Name, $"%{keyword}%"));
        }

        if (query.AcademicContextId is not null)
        {
            // A key that is not a scope matches nothing, as an unknown context id did.
            var scope = MatrixScope.TryParse(query.AcademicContextId.Value, out var parsed) ? parsed : default;
            matrices = matrices.Where(matrix =>
                matrix.BranchId == scope.BranchId &&
                matrix.SubjectId == scope.SubjectId &&
                matrix.GradeLevelId == scope.GradeLevelId);
        }

        if (query.SubjectId is not null)
        {
            matrices = matrices.Where(matrix => matrix.SubjectId == query.SubjectId.Value);
        }

        if (query.GradeLevelId is not null)
        {
            matrices = matrices.Where(matrix => matrix.GradeLevelId == query.GradeLevelId.Value);
        }

        if (query.BranchId is not null)
        {
            matrices = matrices.Where(matrix => matrix.BranchId == query.BranchId.Value);
        }

        // A task's matrix exists from task creation; until the team lead starts it, it is not a matrix to list.
        matrices = matrices.Where(matrix =>
            matrix.Task == null || matrix.Task.Status != "ASSIGNED" ||
            matrix.Status != MatrixStatusCodes.Draft || matrix.Details.Any());

        if (query.HideTaskDrafts)
        {
            matrices = matrices.Where(matrix =>
                matrix.TaskId == null || matrix.Status != MatrixStatusCodes.Draft);
        }

        if (query.AssignedToUserId is not null)
        {
            matrices = matrices.Where(matrix =>
                matrix.Task != null &&
                matrix.Task.AssignedToUserId == query.AssignedToUserId.Value);
        }

        var totalCount = await matrices.CountAsync(cancellationToken);
        var rows = await matrices
            .OrderByDescending(matrix => matrix.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(matrix => new
            {
                matrix.Id,
                matrix.Name,
                matrix.Status,
                matrix.TaskId,
                matrix.BranchId,
                matrix.SubjectId,
                matrix.GradeLevelId,
                matrix.CreatedByUserId,
                TaskCreatorId = matrix.Task == null ? (ulong?)null : matrix.Task.CreatedByUserId,
                SubjectName = matrix.Subject.Name,
                GradeLevelName = matrix.GradeLevel.Name,
                TotalQuestions = matrix.Details
                    .Select(detail => (long?)detail.QuestionCount)
                    .Sum() ?? 0,
                matrix.TotalScore
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new MatrixListRow(
            row.Id,
            row.Name,
            row.Status,
            row.TaskId,
            new MatrixScope(row.BranchId, row.SubjectId, row.GradeLevelId).Key,
            checked((uint)row.TotalQuestions),
            row.TotalScore,
            row.CreatedByUserId,
            // Same rule as ExamMatrix.ApprovedByUserId: reviewer = task creator, direct matrix = its author.
            row.Status is MatrixStatusCodes.Approved or MatrixStatusCodes.Archived
                ? row.TaskId is null ? row.CreatedByUserId : row.TaskCreatorId
                : null,
            row.SubjectName, row.GradeLevelName)).ToArray();

        return new PagedResult<MatrixListRow>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ExamMatrix?> GetAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var matrix = await Db.ExamMatrices
            .Include(item => item.Task)
            .Include(item => item.Details)
                .ThenInclude(detail => detail.Lesson)
                    .ThenInclude(lesson => lesson.Chapter)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (matrix is not null)
        {
            matrix.Details = matrix.Details
                .OrderBy(detail => detail.Lesson.ChapterId)
                .ThenBy(detail => detail.LessonId)
                .ThenBy(detail => detail.CognitiveLevel)
                .ThenBy(detail => detail.QuestionType)
                .ToList();
        }

        return matrix;
    }

    // The task was loaded without tracking; attach it so EF does not try to insert it again.
    public override async Task AddAsync(
        ExamMatrix matrix,
        CancellationToken cancellationToken = default)
    {
        if (matrix.Task is not null &&
            Db.Entry(matrix.Task).State == EntityState.Detached)
        {
            Db.Attach(matrix.Task);
        }

        await Db.ExamMatrices.AddAsync(matrix, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<ulong, MatrixPersonRow>> GetPeopleAsync(
        IReadOnlyCollection<ulong> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<ulong, MatrixPersonRow>();
        }

        var users = await Db.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        var roles = await Db.UserRoles
            .AsNoTracking()
            .WhereEffective()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Select(userRole => new { userRole.UserId, userRole.Role.Code })
            .ToListAsync(cancellationToken);

        var rolesByUser = roles
            .GroupBy(role => role.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(role => role.Code).ToArray());

        return users.ToDictionary(
            user => user.Id,
            user => new MatrixPersonRow(
                user.Id,
                user.FullName,
                rolesByUser.TryGetValue(user.Id, out var codes) ? codes : Array.Empty<string>()));
    }

    public async Task<bool> TryUpdateStatusAsync(
        ExamMatrix matrix,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        var affected = await Db.ExamMatrices
            .Where(item => item.Id == matrix.Id && item.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Status, matrix.Status),
                cancellationToken);

        if (affected != 1)
        {
            return false;
        }

        // The column is already written; stop EF from writing it again.
        var entry = Db.Entry(matrix);
        entry.Property(item => item.Status).OriginalValue = matrix.Status;
        entry.Property(item => item.Status).IsModified = false;
        return true;
    }

    // Row lock (SELECT ... FOR UPDATE) that also proves the status is still the one the caller read.
    public async Task<bool> LockWithStatusAsync(
        ulong matrixId,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        var rows = await Db.Database
            .SqlQuery<ulong>($"SELECT matrix_id AS Value FROM matrix WHERE matrix_id = {matrixId} AND status = {expectedStatus} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.Count == 1;
    }

    public async Task SetTaskStatusAsync(
        ulong taskId,
        string status,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await Db.WorkTasks
            .Where(task => task.Id == taskId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.Status, status)
                    .SetProperty(task => task.UpdatedAt, now),
                cancellationToken);
    }

    public void AddTaskHistory(TaskHistory history) => Db.TaskHistories.Add(history);

    public Task<TaskHistory?> GetLatestTaskHistoryAsync(ulong taskId, CancellationToken cancellationToken) =>
        Db.TaskHistories.AsNoTracking()
            .Where(history => history.TaskId == taskId)
            .OrderByDescending(history => history.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static void ValidatePage(MatrixListFilter query)
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
