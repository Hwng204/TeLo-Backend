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
            matrices = matrices.Where(matrix =>
                matrix.AcademicContextId == query.AcademicContextId.Value);
        }

        if (query.SemesterId is not null)
        {
            matrices = matrices.Where(matrix =>
                matrix.SemesterId == query.SemesterId.Value);
        }

        if (query.BranchId is not null)
        {
            matrices = matrices.Where(matrix =>
                matrix.AcademicContext.SchoolBranchId == query.BranchId.Value);
        }

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
                matrix.AcademicContextId,
                matrix.SemesterId,
                matrix.Code,
                matrix.CreatedByUserId,
                matrix.CreatedAt,
                matrix.ApprovedByUserId,
                matrix.ApprovedAt,
                TotalQuestions = matrix.Details
                    .Select(detail => (long?)detail.QuestionCount)
                    .Sum() ?? 0,
                TotalScore = matrix.Details
                    .Select(detail => (decimal?)detail.AllocatedScore)
                    .Sum() ?? 0m
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new MatrixListRow(
            row.Id,
            row.Name,
            row.Status,
            row.TaskId,
            row.AcademicContextId,
            row.SemesterId,
            checked((uint)row.TotalQuestions),
            row.TotalScore,
            row.Code,
            row.CreatedByUserId,
            row.CreatedAt,
            row.ApprovedByUserId,
            row.ApprovedAt)).ToArray();

        return new PagedResult<MatrixListRow>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<ExamMatrix?> GetAsync(
        ulong id,
        CancellationToken cancellationToken)
    {
        var matrix = await Db.ExamMatrices
            .Include(item => item.Task)
            .Include(item => item.AcademicContext)
            .Include(item => item.Details)
                .ThenInclude(detail => detail.Lesson)
                    .ThenInclude(lesson => lesson.Chapter)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (matrix is not null)
        {
            matrix.Details = matrix.Details
                .OrderBy(detail => detail.Lesson.Chapter.SortOrder)
                .ThenBy(detail => detail.Lesson.SortOrder)
                .ThenBy(detail => detail.CognitiveLevel)
                .ThenBy(detail => detail.QuestionType)
                .ToList();
        }

        return matrix;
    }

    public Task<bool> ExistsForTaskAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return Db.ExamMatrices.AnyAsync(
            matrix => matrix.TaskId == taskId,
            cancellationToken);
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

        if (matrix.CreatedAt == default)
        {
            matrix.CreatedAt = DateTime.UtcNow;
        }

        if (string.IsNullOrEmpty(matrix.Code))
        {
            matrix.Code = await NextCodeAsync(matrix.CreatedAt, cancellationToken);
        }

        await Db.ExamMatrices.AddAsync(matrix, cancellationToken);
    }

    /// <summary>
    /// Next code MT-{year}-{running number}. The counter row is bumped atomically with
    /// LAST_INSERT_ID(expr), inside the caller's transaction: two concurrent creates can never read
    /// the same number, and a rolled-back create gives its number back (no gaps).
    /// </summary>
    private async Task<string> NextCodeAsync(DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        // Vietnam is UTC+7; the code year follows the local calendar year, not UTC.
        var year = createdAtUtc.AddHours(7).Year;

        // LAST_INSERT_ID() belongs to one connection. Inside the service's transaction that is guaranteed,
        // but outside one EF would open a fresh connection per command and read back 0, so pin a single
        // connection for the two statements when no transaction has already done so.
        var connection = Db.Database.GetDbConnection();
        var pinnedHere = connection.State != System.Data.ConnectionState.Open;
        if (pinnedHere)
        {
            await Db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await Db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO matrix_code_sequences (year_number, last_number) VALUES ({year}, LAST_INSERT_ID(1))
                   ON DUPLICATE KEY UPDATE last_number = LAST_INSERT_ID(last_number + 1)",
                cancellationToken);

            var number = await Db.Database
                .SqlQuery<ulong>($"SELECT LAST_INSERT_ID() AS Value")
                .SingleAsync(cancellationToken);

            return $"MT-{year}-{number:D3}";
        }
        finally
        {
            if (pinnedHere)
            {
                await Db.Database.CloseConnectionAsync();
            }
        }
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
                setters => setters
                    .SetProperty(item => item.Status, matrix.Status)
                    .SetProperty(item => item.RejectComment, matrix.RejectComment)
                    .SetProperty(item => item.RejectedByUserId, matrix.RejectedByUserId)
                    .SetProperty(item => item.RejectedAt, matrix.RejectedAt)
                    .SetProperty(item => item.ApprovedByUserId, matrix.ApprovedByUserId)
                    .SetProperty(item => item.ApprovedAt, matrix.ApprovedAt),
                cancellationToken);

        if (affected != 1)
        {
            return false;
        }

        // The columns are already written; stop EF from writing them again.
        var entry = Db.Entry(matrix);
        entry.Property(item => item.Status).OriginalValue = matrix.Status;
        entry.Property(item => item.RejectComment).OriginalValue = matrix.RejectComment;
        entry.Property(item => item.RejectedByUserId).OriginalValue = matrix.RejectedByUserId;
        entry.Property(item => item.RejectedAt).OriginalValue = matrix.RejectedAt;
        entry.Property(item => item.ApprovedByUserId).OriginalValue = matrix.ApprovedByUserId;
        entry.Property(item => item.ApprovedAt).OriginalValue = matrix.ApprovedAt;
        entry.Property(item => item.Status).IsModified = false;
        entry.Property(item => item.RejectComment).IsModified = false;
        entry.Property(item => item.RejectedByUserId).IsModified = false;
        entry.Property(item => item.RejectedAt).IsModified = false;
        entry.Property(item => item.ApprovedByUserId).IsModified = false;
        entry.Property(item => item.ApprovedAt).IsModified = false;
        return true;
    }

    // Row lock (SELECT ... FOR UPDATE) that also proves the status is still the one the caller read.
    public async Task<bool> LockWithStatusAsync(
        ulong matrixId,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        var rows = await Db.Database
            .SqlQuery<ulong>($"SELECT id AS Value FROM exam_matrices WHERE id = {matrixId} AND status = {expectedStatus} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.Count == 1;
    }

    public async Task SetTaskStatusAsync(
        ulong taskId,
        string status,
        ulong updatedByUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await Db.WorkTasks
            .Where(task => task.Id == taskId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.Status, status)
                    .SetProperty(task => task.UpdatedAt, now)
                    .SetProperty(task => task.UpdatedByUserId, updatedByUserId),
                cancellationToken);
    }

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
