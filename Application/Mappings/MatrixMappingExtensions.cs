using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;
using Infrastructure.Models;

namespace Application.Mappings;

public static class MatrixMappingExtensions
{
    public static MatrixListFilter ToFilter(this MatrixListQuery query)
    {
        return new MatrixListFilter(
            query.Page,
            query.PageSize,
            query.Keyword,
            query.AcademicContextId,
            query.Status,
            query.AssignedToUserId,
            query.BranchId,
            SubjectId: query.SubjectId,
            GradeLevelId: query.GradeLevelId);
    }

    public static MatrixPage ToDto(
        this PagedResult<MatrixListRow> page,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null)
    {
        var items = page.Items
            .Select(row => new MatrixListItem(
                row.Id,
                row.Name,
                row.Status,
                row.TaskId,
                row.AcademicContextId,
                row.TotalQuestions,
                row.TotalScore,
                Person(people, row.CreatedByUserId),
                Person(people, row.ApprovedByUserId),
                row.SubjectName, row.GradeLevelName))
            .ToArray();

        return new MatrixPage(items, page.Page, page.PageSize, page.TotalCount);
    }

    public static MatrixWorkbookModel ToWorkbookModel(
        this MatrixResponse matrix,
        MatrixExportInfo info)
    {
        var rows = matrix.Details
            .Select(detail => new MatrixWorkbookRow(
                info.LessonTitles.TryGetValue(detail.LessonId, out var title)
                    ? title
                    : detail.LessonId.ToString(),
                detail.CognitiveLevel,
                detail.QuestionType,
                detail.QuestionCount,
                detail.Percentage,
                detail.CellScore,
                detail.LessonId))
            .ToArray();

        return new MatrixWorkbookModel(
            matrix.Name,
            matrix.Status,
            matrix.TaskId,
            info.ContextLabel,
            matrix.TotalQuestions,
            matrix.TotalScore,
            rows);
    }

    public static IReadOnlyList<MatrixDetailValue> ToValues(
        this IReadOnlyCollection<MatrixDetailRequest> details)
    {
        return details
            .Select(detail => new MatrixDetailValue(
                detail.LessonId,
                detail.CognitiveLevel,
                MatrixQuestionTypes.MultipleChoice,
                detail.QuestionCount,
                detail.Percentage))
            .ToArray();
    }

    /// <summary>Looks a user up in a resolved set; null when there is no such id or it was not resolved.</summary>
    public static MatrixPerson? Person(IReadOnlyDictionary<ulong, MatrixPerson>? people, ulong? userId)
    {
        return userId is not null && people is not null && people.TryGetValue(userId.Value, out var person)
            ? person
            : null;
    }

    /// <summary>Ids of the people a matrix response will show, for one batched lookup.</summary>
    public static IEnumerable<ulong?> PeopleIds(this ExamMatrix matrix)
    {
        yield return matrix.CreatedByUserId;
        yield return matrix.ApprovedByUserId;
    }

    // lastHistory: newest task_history row of the matrix's task; it is the pending rejection while the matrix is a Draft.
    public static MatrixResponse ToResponse(
        this ExamMatrix matrix,
        MatrixActor actor,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null,
        TaskHistory? lastHistory = null)
    {
        // Only a draft sent back (task NEEDS_REVISION) shows it: a draft restarted after deleting the returned one does not.
        var rejection = matrix is { Status: MatrixStatusCodes.Draft, Task.Status: MatrixTaskStatusCodes.NeedsRevision } ? lastHistory : null;

        // Lesson/chapter names come from the loaded rows, so archived (INACTIVE) ones still show; null when not loaded.
        var details = matrix.Details
            .Select(detail => new MatrixDetailResponse(
                detail.Id,
                detail.LessonId,
                detail.CognitiveLevel,
                detail.QuestionType,
                detail.QuestionCount,
                detail.Percentage,
                matrix.TotalScore * detail.Percentage / 100m,
                detail.Lesson?.Title,
                detail.Lesson?.Number,
                detail.Lesson?.ChapterId,
                detail.Lesson?.Chapter?.Title,
                detail.Lesson?.Chapter?.Number))
            .ToArray();

        return new MatrixResponse(
            matrix.Id,
            matrix.Name,
            matrix.Status,
            matrix.TaskId,
            matrix.Scope.Key,
            details,
            matrix.TotalQuestions,
            matrix.TotalScore,
            AllowedActions(matrix, actor),
            string.IsNullOrEmpty(rejection?.Content) ? null : rejection.Content,
            rejection?.CreateAt,
            rejection?.CreateBy,
            Person(people, matrix.CreatedByUserId),
            Person(people, matrix.ApprovedByUserId));
    }

    private static IReadOnlyList<string> AllowedActions(
        ExamMatrix matrix,
        MatrixActor actor)
    {
        var actions = new List<string> { "View" };

        if (matrix.CanEdit(actor))
        {
            actions.Add("Update");
        }

        if (matrix.CanHardDelete(actor))
        {
            actions.Add("Delete");
        }

        if (matrix.Status == MatrixStatusCodes.Draft)
        {
            // Submit/Confirm are offered only once the matrix's rows add up to exactly 100%, so the
            // UI never shows a button the domain would reject with InvalidTotalScore.
            if (matrix.Details.Count > 0 && matrix.HasRequiredTotalPercentage)
            {
                // A direct matrix is confirmed by its author only; a task matrix is submitted to its reviewer.
                if (matrix.TaskId is null && matrix.CanReview(actor))
                {
                    actions.Add("Confirm");
                }

                if (matrix.TaskId is not null)
                {
                    actions.Add("Submit");
                }
            }
        }
        else if (matrix.Status == MatrixStatusCodes.Submitted)
        {
            if (matrix.CanReview(actor))
            {
                actions.Add("Approve");
                actions.Add("Reject");
            }
        }
        else if (matrix.Status == MatrixStatusCodes.Approved)
        {
            if (actor.Role == MatrixActorRole.Pht)
            {
                actions.Add("Archive");
                actions.Add("Clone");
            }

            actions.Add("Export");
        }
        else if (matrix.Status == MatrixStatusCodes.Archived)
        {
            if (actor.Role == MatrixActorRole.Pht)
            {
                actions.Add("Clone");
            }

            actions.Add("Export");
        }

        return actions;
    }
}

public static class MatrixTaskMappingExtensions
{
    public static MatrixReferenceData ToDto(this MatrixReferenceModel model)
    {
        return new MatrixReferenceData(
            model.AcademicContexts,
            model.Lessons,
            model.TeamLeads);
    }

    // academicContextId is the scope key of the task's matrix; matrixId only once the matrix is started.
    public static MatrixTaskResponse ToResponse(
        this WorkTask task,
        ulong academicContextId,
        ulong? matrixId,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null)
    {
        return new MatrixTaskResponse(
            task.Id,
            task.AssignedToUserId,
            academicContextId,
            task.DueAt,
            task.Status,
            task.TaskType,
            task.Description,
            matrixId,
            task.Name,
            MatrixMappingExtensions.Person(people, task.CreatedByUserId));
    }
}
