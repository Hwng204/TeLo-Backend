using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

// "Not started" (GB-55, GB-56) for every task type, shared by TaskService and the matrix-task endpoints.
// A matrix task's matrix and an exam task's sets exist from creation, so only their content counts.
public static class TaskProgressQuery
{
    public static IQueryable<WorkTask> WhereNotStarted(this IQueryable<WorkTask> tasks, ApplicationDbContext db) =>
        tasks.Where(task =>
            task.Status == "ASSIGNED" &&
            !db.ExamMatrices.Any(matrix => matrix.TaskId == task.Id &&
                (matrix.Status != MatrixStatusCodes.Draft || matrix.Details.Any())) &&
            !db.ExamSets.Any(set => set.TaskId == task.Id &&
                (set.Status != ExamSetStatusCodes.Draft || set.Questions.Any())) &&
            !db.Questions.Any(question => question.QuestionTaskDetail.QuestionTask.TaskId == task.Id));

    // Row lock on the task: whoever starts or edits it waits for the other.
    public static async Task LockTaskAsync(this ApplicationDbContext db, ulong taskId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<ulong>($"SELECT task_id AS Value FROM task WHERE task_id = {taskId} FOR UPDATE")
            .ToListAsync(cancellationToken);

    // False when the task is gone or started. matrix.task_id and exam_set.task_id are SET NULL, so the
    // task's empty matrix and exam sets are deleted with it instead of being left without a task.
    public static async Task<bool> DeleteTaskIfNotStartedAsync(
        this ApplicationDbContext db,
        ulong taskId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockTaskAsync(taskId, cancellationToken);
        if (!await db.WorkTasks.Where(task => task.Id == taskId).WhereNotStarted(db).AnyAsync(cancellationToken))
            return false;

        await db.ExamMatrices.Where(matrix => matrix.TaskId == taskId).ExecuteDeleteAsync(cancellationToken);
        await db.ExamSets.Where(set => set.TaskId == taskId).ExecuteDeleteAsync(cancellationToken);
        await db.WorkTasks.Where(task => task.Id == taskId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
