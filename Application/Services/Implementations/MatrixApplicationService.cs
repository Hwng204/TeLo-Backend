using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class MatrixApplicationService(
    IUnitOfWork uow,
    IMatrixCurrentUser currentUser,
    IMatrixWorkbookExporter exporter,
    IMatrixPeopleResolver peopleResolver,
    IEmailNotificationService emailNotifications) : IMatrixApplicationService
{
    // matrix.total_score is decimal(5,2).
    internal const int MaxTotalScore = 999;

    public async Task<MatrixPage> ListAsync(
        MatrixListQuery query,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var scopedQuery = actor.Role == MatrixActorRole.TeamLead
            ? query with { AssignedToUserId = actor.UserId }
            : query with { BranchId = BranchScope(actor) };

        var filter = scopedQuery.ToFilter() with { HideTaskDrafts = actor.Role == MatrixActorRole.Pht };
        var page = await uow.Matrices.ListAsync(filter, cancellationToken);
        var people = await peopleResolver.ResolveAsync(
            page.Items.SelectMany(item => new[] { item.CreatedByUserId, item.ApprovedByUserId }),
            cancellationToken);
        return page.ToDto(people);
    }

    public async Task<MatrixResponse> CreateAsync(
        SaveMatrixRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var actor = currentUser.Actor;

        return await uow.ExecuteInTransactionAsync(async ct =>
        {
            var matrix = await BuildNewMatrixAsync(request, actor, ct);
            // A task's matrix row already exists (created with the task) and is filled in place.
            if (matrix.Id == 0)
                await uow.Matrices.AddAsync(matrix, ct);
            await uow.CompleteAsync(ct);
            // Tổ trưởng lưu bản nháp đầu tiên của nhiệm vụ: nhiệm vụ chuyển Đang thực hiện.
            if (matrix.TaskId is { } taskId)
                await uow.Matrices.SetTaskStatusAsync(taskId, MatrixTaskStatusCodes.InProgress, ct);
            return await ToResponseAsync(matrix, actor, ct);
        }, cancellationToken);
    }

    public async Task<MatrixResponse> GetAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var matrix = await GetRequiredAsync(matrixId, actor, cancellationToken);
        EnsureViewAccess(matrix, actor);
        return await ToResponseAsync(matrix, actor, cancellationToken);
    }

    public async Task<MatrixResponse> UpdateAsync(
        ulong matrixId,
        SaveMatrixRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var actor = currentUser.Actor;

        return await uow.ExecuteInTransactionAsync(async ct =>
        {
            var matrix = await GetRequiredAsync(matrixId, actor, ct);
            if (request.TaskId != matrix.TaskId)
            {
                throw new MatrixApplicationException(
                    "TaskImmutable",
                    "Không thể chuyển ma trận giữa dạng trực tiếp và dạng giao nhiệm vụ.");
            }

            // A task matrix keeps the scope the task gave it.
            var scope = matrix.Scope;
            if (matrix.TaskId is not null)
            {
                var task = await uow.MatrixTasks.GetAsync(matrix.TaskId.Value, ct);
                if (task is null)
                {
                    throw new MatrixApplicationException(
                        "TaskNotFound",
                        "Không tìm thấy nhiệm vụ ma trận.");
                }

                EnsureTaskAccess(task, actor);
            }
            else
            {
                scope = ParseScope(request.AcademicContextId);
            }

            await uow.MatrixReferences.EnsureValidAsync(
                scope,
                LessonIds(request.Details),
                ct,
                BranchScope(actor));

            await EnsureUnchangedAsync(matrix, ct);
            matrix.Name = request.Name.Trim();
            matrix.BranchId = scope.BranchId;
            matrix.SubjectId = scope.SubjectId;
            matrix.GradeLevelId = scope.GradeLevelId;
            matrix.TotalScore = request.TotalScore;
            ReplaceDetails(matrix, request.Details, actor);
            await uow.CompleteAsync(ct);
            return await ToResponseAsync(matrix, actor, ct);
        }, cancellationToken);
    }

    public async Task DeleteDraftAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            var matrix = await GetRequiredAsync(matrixId, actor, ct);
            if (matrix.Status != MatrixStatusCodes.Draft)
            {
                throw new MatrixApplicationException(
                    "InvalidTransition",
                    "Chỉ được xóa ma trận ở trạng thái Nháp.");
            }

            if (!matrix.CanHardDelete(actor))
            {
                throw new MatrixApplicationException(
                    "Forbidden",
                    "Bạn không có quyền xóa ma trận Nháp này.");
            }

            await EnsureUnchangedAsync(matrix, ct);
            var taskId = matrix.TaskId;
            // A task's matrix row holds the task's scope: emptying it un-starts the task.
            if (taskId is null)
                await uow.Matrices.DeleteAsync(matrix);
            else
                matrix.Details.Clear();
            await uow.CompleteAsync(ct);
            // Không còn bản nháp nào: nhiệm vụ về lại Đã giao.
            if (taskId is not null)
                await uow.Matrices.SetTaskStatusAsync(taskId.Value, MatrixTaskStatusCodes.Assigned, ct);
            return true;
        }, cancellationToken);
    }

    public Task<MatrixResponse> SubmitAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            matrixId,
            (matrix, actor) => matrix.Submit(actor),
            MatrixTaskStatusCodes.PendingReview,
            cancellationToken);
    }

    public Task<MatrixResponse> RejectAsync(
        ulong matrixId,
        RejectMatrixRequest? request,
        CancellationToken cancellationToken)
    {
        var comment = request?.Comment;
        if (comment is not null && comment.Trim().Length > 1000)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Nhận xét từ chối tối đa 1000 ký tự.");
        }

        return TransitionAsync(
            matrixId,
            (matrix, actor) => matrix.Reject(actor),
            MatrixTaskStatusCodes.NeedsRevision,
            cancellationToken,
            historyContent: comment?.Trim() ?? string.Empty);
    }

    public Task<MatrixResponse> ApproveAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            matrixId,
            (matrix, actor) => matrix.Approve(actor),
            MatrixTaskStatusCodes.Completed,
            cancellationToken);
    }

    public Task<MatrixResponse> ConfirmDirectAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            matrixId,
            (matrix, actor) => matrix.ConfirmDirect(actor),
            null,
            cancellationToken);
    }

    public Task<MatrixResponse> ArchiveAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        return TransitionAsync(
            matrixId,
            (matrix, actor) => matrix.Archive(actor),
            null,
            cancellationToken);
    }

    public async Task<MatrixResponse> CloneAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;

        return await uow.ExecuteInTransactionAsync(async ct =>
        {
            var matrix = await GetRequiredAsync(matrixId, actor, ct);
            var clone = matrix.CloneAsDraft(actor);
            await uow.Matrices.AddAsync(clone, ct);
            await uow.CompleteAsync(ct);
            return await ToResponseAsync(clone, actor, ct);
        }, cancellationToken);
    }

    private async Task<MatrixResponse> TransitionAsync(
        ulong matrixId,
        Action<ExamMatrix, MatrixActor> transition,
        string? taskStatusAfter,
        CancellationToken cancellationToken,
        string? historyContent = null)
    {
        var actor = currentUser.Actor;

        return await uow.ExecuteInTransactionAsync(async ct =>
        {
            var matrix = await GetRequiredAsync(matrixId, actor, ct);
            var expectedStatus = matrix.Status;
            try
            {
                transition(matrix, actor);
            }
            catch (MatrixDomainException exception)
            {
                throw new MatrixApplicationException(exception.Code, exception.Message);
            }

            if (!await uow.Matrices.TryUpdateStatusAsync(matrix, expectedStatus, ct))
            {
                throw new MatrixApplicationException(
                    "ConcurrencyConflict",
                    "Trạng thái ma trận đã bị thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
            }

            if (taskStatusAfter is not null && matrix.TaskId is not null)
            {
                await uow.Matrices.SetTaskStatusAsync(
                    matrix.TaskId.Value,
                    taskStatusAfter,
                    ct);
            }

            // A direct matrix has no task to keep the note on, so its reject comment is not stored.
            if (historyContent is not null && matrix.TaskId is not null)
            {
                uow.Matrices.AddTaskHistory(new TaskHistory
                {
                    TaskId = matrix.TaskId.Value,
                    Content = historyContent,
                    CreateBy = actor.UserId,
                    CreateAt = DateTime.UtcNow
                });
                await uow.CompleteAsync(ct);
            }

            var eventCode = matrix.Status switch
            {
                MatrixStatusCodes.Submitted => "MATRIX_SUBMITTED",
                MatrixStatusCodes.Approved => "MATRIX_APPROVED",
                MatrixStatusCodes.Draft when expectedStatus == MatrixStatusCodes.Submitted => "MATRIX_REJECTED",
                _ => null
            };
            if (eventCode != null) await emailNotifications.QueueMatrixAsync(matrix.Id, eventCode, actor.UserId, ct);

            return await ToResponseAsync(matrix, actor, ct);
        }, cancellationToken);
    }

    public async Task<MatrixExportFile> ExportAsync(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var matrix = await GetAsync(matrixId, cancellationToken);
        if (!matrix.AllowedActions.Contains("Export"))
        {
            throw new MatrixApplicationException(
                "InvalidTransition",
                "Chỉ xuất được ma trận đã duyệt hoặc đã lưu trữ.");
        }

        var info = await uow.MatrixReferences.GetExportInfoAsync(
            ParseScope(matrix.AcademicContextId),
            matrix.Details.Select(detail => detail.LessonId).Distinct().ToArray(),
            cancellationToken);

        return new MatrixExportFile(
            $"{FileNames.Safe(matrix.Name, "matrix")}.xlsx",
            exporter.Create(matrix.ToWorkbookModel(info)));
    }

    // Names the people a matrix response shows (author, approver) with one batched lookup; a returned
    // task draft also shows its last rejection from task_history.
    private async Task<MatrixResponse> ToResponseAsync(
        ExamMatrix matrix,
        MatrixActor actor,
        CancellationToken cancellationToken)
    {
        var people = await peopleResolver.ResolveAsync(matrix.PeopleIds(), cancellationToken);
        var lastHistory = matrix is { Status: MatrixStatusCodes.Draft, TaskId: { } taskId }
            ? await uow.Matrices.GetLatestTaskHistoryAsync(taskId, cancellationToken)
            : null;
        return matrix.ToResponse(actor, people, lastHistory);
    }

    private static IReadOnlyCollection<ulong> LessonIds(IEnumerable<MatrixDetailRequest> details)
    {
        return details.Select(detail => detail.LessonId).ToArray();
    }

    // A PHT is limited to their own branch; the Principal and Team Leads are not branch-limited here.
    internal static ulong? BranchScope(MatrixActor actor)
    {
        if (actor.Role != MatrixActorRole.Pht || actor.IsPrincipal)
        {
            return null;
        }

        return actor.BranchId ?? throw new MatrixApplicationException(
            "Forbidden",
            "Tài khoản PHT chưa được gán chi nhánh.");
    }

    private async Task EnsureUnchangedAsync(ExamMatrix matrix, CancellationToken cancellationToken)
    {
        if (!await uow.Matrices.LockWithStatusAsync(matrix.Id, matrix.Status, cancellationToken))
        {
            throw new MatrixApplicationException(
                "ConcurrencyConflict",
                "Ma trận đã bị thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }
    }

    private async Task<ExamMatrix> BuildNewMatrixAsync(
        SaveMatrixRequest request,
        MatrixActor actor,
        CancellationToken cancellationToken)
    {
        if (request.TaskId is null)
        {
            if (actor.Role != MatrixActorRole.Pht)
            {
                throw new MatrixApplicationException(
                    "Forbidden",
                    "Chỉ PHT mới được tạo ma trận trực tiếp.");
            }

            var scope = ParseScope(request.AcademicContextId);
            await uow.MatrixReferences.EnsureValidAsync(
                scope,
                LessonIds(request.Details),
                cancellationToken,
                BranchScope(actor));

            var directMatrix = new ExamMatrix
            {
                Name = request.Name.Trim(),
                Status = MatrixStatusCodes.Draft,
                TaskId = null,
                BranchId = scope.BranchId,
                SubjectId = scope.SubjectId,
                GradeLevelId = scope.GradeLevelId,
                TotalScore = request.TotalScore,
                CreatedByUserId = actor.UserId
            };

            ReplaceDetails(directMatrix, request.Details, actor);
            return directMatrix;
        }

        await uow.MatrixTasks.LockAsync(request.TaskId.Value, cancellationToken);
        var task = await uow.MatrixTasks.GetAsync(request.TaskId.Value, cancellationToken);
        if (task is null)
        {
            throw new MatrixApplicationException(
                "TaskNotFound",
                "Không tìm thấy nhiệm vụ ma trận.");
        }

        EnsureTaskAccess(task, actor);

        if (!string.Equals(task.TaskType, "MATRIX", StringComparison.OrdinalIgnoreCase))
        {
            throw new MatrixApplicationException(
                "InvalidTaskType",
                "Nhiệm vụ được chọn không phải nhiệm vụ ma trận.");
        }

        var taskMatrix = await uow.MatrixTasks.GetTaskMatrixAsync(task.Id, cancellationToken)
            ?? throw new MatrixApplicationException(
                "TaskScopeRequired",
                "Nhiệm vụ ma trận không có phạm vi ma trận.");
        if (taskMatrix.Started)
        {
            throw new MatrixApplicationException(
                "TaskAlreadyHasMatrix",
                "Nhiệm vụ này đã có ma trận.");
        }

        await uow.MatrixReferences.EnsureValidAsync(
            taskMatrix.Scope,
            LessonIds(request.Details),
            cancellationToken,
            BranchScope(actor));

        var delegatedMatrix = await uow.Matrices.GetAsync(taskMatrix.Id, cancellationToken)
            ?? throw new MatrixApplicationException("TaskNotFound", "Không tìm thấy nhiệm vụ ma trận.");
        delegatedMatrix.Name = request.Name.Trim();
        delegatedMatrix.TotalScore = request.TotalScore;
        ReplaceDetails(delegatedMatrix, request.Details, actor);
        return delegatedMatrix;
    }

    private async Task<ExamMatrix> GetRequiredAsync(
        ulong matrixId,
        MatrixActor actor,
        CancellationToken cancellationToken)
    {
        var matrix = await uow.Matrices.GetAsync(matrixId, cancellationToken);
        // An empty draft of a still-ASSIGNED task is the row created with the task: clients see it only
        // once POST /matrices fills it.
        if (matrix is null or
            { Status: MatrixStatusCodes.Draft, Details.Count: 0, Task.Status: MatrixTaskStatusCodes.Assigned })
        {
            throw new MatrixApplicationException(
                "NotFound",
                "Không tìm thấy ma trận.");
        }

        var branchScope = BranchScope(actor);
        if (branchScope is not null &&
            matrix.BranchId != branchScope)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Ma trận thuộc chi nhánh khác.");
        }

        // Bản Nháp của Tổ trưởng là việc riêng của họ cho tới khi nộp: PHT không xem, sửa, xoá được.
        if (actor.Role == MatrixActorRole.Pht &&
            matrix.Status == MatrixStatusCodes.Draft &&
            matrix.TaskId is not null)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Ma trận đang được Tổ trưởng soạn, chỉ xem được sau khi nộp.");
        }

        return matrix;
    }

    private static void EnsureViewAccess(ExamMatrix matrix, MatrixActor actor)
    {
        if (actor.Role == MatrixActorRole.Pht)
        {
            return;
        }

        if (actor.Role == MatrixActorRole.TeamLead &&
            matrix.Task is not null &&
            matrix.Task.AssignedToUserId == actor.UserId)
        {
            return;
        }

        throw new MatrixApplicationException(
            "Forbidden",
            "Bạn không có quyền xem ma trận này.");
    }

    private static void ReplaceDetails(
        ExamMatrix matrix,
        IReadOnlyCollection<MatrixDetailRequest> details,
        MatrixActor actor)
    {
        try
        {
            matrix.ReplaceDetails(details.ToValues(), actor);
        }
        catch (MatrixDomainException exception)
        {
            throw new MatrixApplicationException(exception.Code, exception.Message);
        }
    }

    // academicContextId in requests is a MatrixScope.Key.
    internal static MatrixScope ParseScope(ulong academicContextId)
    {
        return MatrixScope.TryParse(academicContextId, out var scope)
            ? scope
            : throw new MatrixApplicationException(
                "InvalidReference",
                "Ngữ cảnh học thuật không hợp lệ.");
    }

    private static void EnsureTaskAccess(WorkTask task, MatrixActor actor)
    {
        if (!(actor.Role == MatrixActorRole.Pht && task.CreatedByUserId == actor.UserId) &&
            !(actor.Role == MatrixActorRole.TeamLead && task.AssignedToUserId == actor.UserId))
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Bạn không phải người giao hoặc Tổ trưởng được giao nhiệm vụ này.");
        }
    }

    private static void ValidateRequest(SaveMatrixRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Tên ma trận là bắt buộc.");
        }

        if (request.Details is null)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Chi tiết ma trận là bắt buộc.");
        }

        if (request.TotalScore is <= 0 or > MaxTotalScore)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                $"Tổng điểm ma trận phải là số nguyên dương, tối đa {MaxTotalScore}.");
        }

        if (request.Name.Trim().Length > 255 ||
            request.Details.Any(detail =>
                detail is null ||
                detail.CognitiveLevel?.Trim().Length > 50 ||
                detail.Percentage <= 0 ||
                detail.Percentage > 100))
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Tên ma trận (tối đa 255 ký tự), mức nhận thức (tối đa 50 ký tự) hoặc tỷ lệ điểm (phải trong khoảng (0, 100]) vượt quá giới hạn cho phép.");
        }

        if (request.Details.Sum(detail => (long)detail.QuestionCount) > uint.MaxValue)
            throw new MatrixApplicationException("InvalidRequest", $"Tổng số câu tối đa {uint.MaxValue}.");
    }
}
