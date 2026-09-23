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
            query.SemesterId,
            query.Status,
            query.AssignedToUserId,
            query.BranchId,
            AcademicYearId: query.AcademicYearId,
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
                row.SemesterId,
                row.TotalQuestions,
                row.TotalScore,
                Person(people, row.CreatedByUserId),
                row.CreatedByUserId is null && row.CreatedAt == default ? null : row.CreatedAt,
                Person(people, row.ApprovedByUserId),
                row.ApprovedAt))
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
                detail.CellScore))
            .ToArray();

        return new MatrixWorkbookModel(
            matrix.Name,
            matrix.Status,
            matrix.TaskId,
            info.ContextLabel,
            info.SemesterName ?? matrix.SemesterId?.ToString() ?? string.Empty,
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

    public static MatrixResponse ToResponse(
        this ExamMatrix matrix,
        MatrixActor actor,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null)
    {
        var details = matrix.Details
            .Select(detail => new MatrixDetailResponse(
                detail.Id,
                detail.LessonId,
                detail.CognitiveLevel,
                detail.QuestionType,
                detail.QuestionCount,
                detail.Percentage,
                matrix.TotalScore * detail.Percentage / 100m))
            .ToArray();

        return new MatrixResponse(
            matrix.Id,
            matrix.Name,
            matrix.Status,
            matrix.TaskId,
            matrix.AcademicContextId,
            matrix.SemesterId,
            details,
            matrix.TotalQuestions,
            matrix.TotalScore,
            AllowedActions(matrix, actor),
            matrix.RejectComment,
            matrix.RejectedAt,
            matrix.RejectedByUserId,
            Person(people, matrix.CreatedByUserId),
            matrix.CreatedAt == default ? null : matrix.CreatedAt,
            Person(people, matrix.ApprovedByUserId),
            matrix.ApprovedAt);
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
                if (actor.Role == MatrixActorRole.Pht && matrix.TaskId is null)
                {
                    actions.Add("Confirm");
                }

                if (actor.Role == MatrixActorRole.Pht || matrix.TaskId is not null)
                {
                    actions.Add("Submit");
                }
            }
        }
        else if (matrix.Status == MatrixStatusCodes.Submitted)
        {
            if (actor.Role == MatrixActorRole.Pht)
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
    public static MatrixTaskFilter ToFilter(this MatrixTaskQuery query)
    {
        return new MatrixTaskFilter(
            query.Page,
            query.PageSize,
            query.Status,
            query.AssignedToUserId,
            query.DueBefore,
            query.BranchId,
            query.Keyword,
            query.AcademicContextId,
            query.AcademicYearId,
            query.SubjectId,
            query.GradeLevelId,
            query.SemesterId);
    }

    public static MatrixTaskPage ToDto(
        this PagedResult<MatrixTaskRow> page,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null)
    {
        var items = page.Items
            .Select(row => new MatrixTaskListItem(
                row.Id,
                row.CreatedByUserId,
                row.AssignedToUserId,
                row.DueAt,
                row.Status,
                row.TaskType,
                row.Description,
                row.AcademicContextId,
                row.SemesterId,
                row.MatrixId,
                row.Name ?? "",
                MatrixMappingExtensions.Person(people, row.CreatedByUserId)))
            .ToArray();

        return new MatrixTaskPage(items, page.Page, page.PageSize, page.TotalCount);
    }

    public static MatrixReferenceData ToDto(this MatrixReferenceModel model)
    {
        var cognitiveLevels = MatrixCognitiveLevels.All
            .Select(level => new MatrixCognitiveLevelOption(level.Code, level.Label))
            .ToArray();

        return new MatrixReferenceData(
            model.AcademicContexts,
            model.Semesters,
            model.Lessons,
            model.TeamLeads,
            cognitiveLevels);
    }

    public static MatrixTaskResponse ToResponse(
        this WorkTask task,
        ulong? matrixId,
        IReadOnlyDictionary<ulong, MatrixPerson>? people = null)
    {
        return new MatrixTaskResponse(
            task.Id,
            task.AssignedToUserId,
            task.AcademicContextId ?? 0,
            task.SemesterId,
            task.DueAt,
            task.Status,
            task.TaskType,
            task.Description,
            matrixId,
            task.Name ?? "",
            MatrixMappingExtensions.Person(people, task.CreatedByUserId));
    }
}
