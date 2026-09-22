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
    IMatrixPeopleResolver peopleResolver) : IMatrixApplicationService
{
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
            await uow.Matrices.AddAsync(matrix, ct);
            await uow.CompleteAsync(ct);
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

            var academicContextId = matrix.AcademicContextId;
            var semesterId = matrix.SemesterId;

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
                academicContextId = RequireTaskContext(task);
                semesterId = task.SemesterId;
            }
            else
            {
                academicContextId = request.AcademicContextId;
                semesterId = request.SemesterId;
            }

            await uow.MatrixReferences.EnsureValidAsync(
                academicContextId,
                semesterId,
                LessonIds(request.Details),
                ct,
                BranchScope(actor));

            await EnsureUnchangedAsync(matrix, ct);
            matrix.Name = request.Name.Trim();
            matrix.AcademicContextId = academicContextId;
            matrix.SemesterId = semesterId;
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
            await uow.Matrices.DeleteAsync(matrix);
            await uow.CompleteAsync(ct);
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
            MatrixTaskStatusCodes.Submitted,
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
            (matrix, actor) => matrix.Reject(actor, comment, DateTime.UtcNow),
            MatrixTaskStatusCodes.Assigned,
            cancellationToken);
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
        CancellationToken cancellationToken)
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
                    actor.UserId,
                    ct);
            }

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
            matrix.AcademicContextId,
            matrix.SemesterId,
            matrix.Details.Select(detail => detail.LessonId).Distinct().ToArray(),
            cancellationToken);

        return new MatrixExportFile(
            $"{FileNames.Safe(matrix.Name, "matrix")}.xlsx",
            exporter.Create(matrix.ToWorkbookModel(info)));
    }

    // Names the people a matrix response shows (author, approver) with one batched lookup.
    private async Task<MatrixResponse> ToResponseAsync(
        ExamMatrix matrix,
        MatrixActor actor,
        CancellationToken cancellationToken)
    {
        var people = await peopleResolver.ResolveAsync(matrix.PeopleIds(), cancellationToken);
        return matrix.ToResponse(actor, people);
    }

    private static IReadOnlyCollection<ulong> LessonIds(IEnumerable<MatrixDetailRequest> details)
    {
        return details.Select(detail => detail.LessonId).ToArray();
    }

    // A PHT is limited to their own branch; the Principal and Team Leads are not branch-limited here.
    private static ulong? BranchScope(MatrixActor actor)
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

            await uow.MatrixReferences.EnsureValidAsync(
                request.AcademicContextId,
                request.SemesterId,
                LessonIds(request.Details),
                cancellationToken,
                BranchScope(actor));

            var directMatrix = new ExamMatrix
            {
                Name = request.Name.Trim(),
                Status = MatrixStatusCodes.Draft,
                TaskId = null,
                SemesterId = request.SemesterId,
                AcademicContextId = request.AcademicContextId,
                CreatedByUserId = actor.UserId,
                CreatedAt = DateTime.UtcNow
            };

            ReplaceDetails(directMatrix, request.Details, actor);
            return directMatrix;
        }

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

        if (await uow.Matrices.ExistsForTaskAsync(task.Id, cancellationToken))
        {
            throw new MatrixApplicationException(
                "TaskAlreadyHasMatrix",
                "Nhiệm vụ này đã có ma trận.");
        }

        var taskContextId = RequireTaskContext(task);
        await uow.MatrixReferences.EnsureValidAsync(
            taskContextId,
            task.SemesterId,
            LessonIds(request.Details),
            cancellationToken,
            BranchScope(actor));

        var delegatedMatrix = new ExamMatrix
        {
            Name = request.Name.Trim(),
            Status = MatrixStatusCodes.Draft,
            TaskId = task.Id,
            Task = task,
            SemesterId = task.SemesterId,
            AcademicContextId = taskContextId,
            CreatedByUserId = actor.UserId,
            CreatedAt = DateTime.UtcNow
        };

        ReplaceDetails(delegatedMatrix, request.Details, actor);
        return delegatedMatrix;
    }

    private async Task<ExamMatrix> GetRequiredAsync(
        ulong matrixId,
        MatrixActor actor,
        CancellationToken cancellationToken)
    {
        var matrix = await uow.Matrices.GetAsync(matrixId, cancellationToken);
        if (matrix is null)
        {
            throw new MatrixApplicationException(
                "NotFound",
                "Không tìm thấy ma trận.");
        }

        var branchScope = BranchScope(actor);
        if (branchScope is not null &&
            matrix.AcademicContext?.SchoolBranchId != branchScope)
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

    private static ulong RequireTaskContext(WorkTask task)
    {
        if (task.AcademicContextId is null)
        {
            throw new MatrixApplicationException(
                "TaskScopeRequired",
                "Nhiệm vụ ma trận phải có ngữ cảnh học thuật.");
        }

        return task.AcademicContextId.Value;
    }

    private static void EnsureTaskAccess(WorkTask task, MatrixActor actor)
    {
        if (actor.Role == MatrixActorRole.TeamLead &&
            task.AssignedToUserId != actor.UserId)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Bạn không phải Tổ trưởng được giao nhiệm vụ này.");
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

        if (request.Name.Trim().Length > 255 ||
            request.Details.Any(detail =>
                detail is null ||
                detail.CognitiveLevel?.Trim().Length > 50 ||
                detail.AllocatedScore > 999.99m))
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Tên ma trận (tối đa 255 ký tự), mức nhận thức (tối đa 50 ký tự) hoặc điểm (tối đa 999,99) vượt quá giới hạn cho phép.");
        }
    }
}
