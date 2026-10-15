using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.QuestionBank;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class MatrixTaskApplicationService(
    IUnitOfWork uow,
    IMatrixCurrentUser currentUser,
    IMatrixPeopleResolver peopleResolver,
    IEmailNotificationService emailNotifications) : IMatrixTaskApplicationService
{
    public async Task<MatrixTaskResponse> CreateAsync(
        CreateMatrixTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            request.AssignedToUserId == 0 ||
            request.AcademicContextId == 0 ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Người nhận, ngữ cảnh học thuật và nội dung nhiệm vụ là bắt buộc.");
        }

        if (request.Name.Trim().Length > 255)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Nội dung nhiệm vụ tối đa 255 ký tự.");
        }

        // GB-52: hạn chọn bằng ô ngày nên so theo ngày, hôm nay vẫn được.
        if (request.DueAt is { } dueAt && dueAt.Date < DateTime.Today)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Hạn hoàn thành không được ở trong quá khứ.");
        }

        var actor = currentUser.Actor;
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Chỉ PHT mới được giao nhiệm vụ ma trận.");
        }

        var scope = MatrixApplicationService.ParseScope(request.AcademicContextId);
        await uow.MatrixReferences.EnsureAssignmentValidAsync(
            actor,
            request.AssignedToUserId,
            scope,
            cancellationToken);

        return await uow.ExecuteInTransactionAsync(async ct =>
        {
            var task = new WorkTask
            {
                CreatedByUserId = actor.UserId,
                AssignedToUserId = request.AssignedToUserId,
                DueAt = request.DueAt,
                Status = MatrixTaskStatusCodes.Assigned,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim(),
                TaskType = "MATRIX",
                CreatedAt = DateTime.UtcNow
            };

            // The task's scope lives on its matrix: an empty DRAFT the team lead fills (POST /matrices).
            await uow.MatrixTasks.AddAsync(task, ct);
            await uow.Matrices.AddAsync(new ExamMatrix
            {
                Task = task,
                Name = task.Name,
                Status = MatrixStatusCodes.Draft,
                BranchId = scope.BranchId,
                SubjectId = scope.SubjectId,
                GradeLevelId = scope.GradeLevelId,
                CreatedByUserId = task.AssignedToUserId,
                TotalScore = 10
            }, ct);
            await uow.CompleteAsync(ct);
            await emailNotifications.QueueMatrixAssignmentAsync(task.Id, actor.UserId, ct);
            return task.ToResponse(scope.Key, null, await peopleResolver.ResolveAsync(new ulong?[] { task.CreatedByUserId }, ct));
        }, cancellationToken);
    }

    public async Task<MatrixTaskPage> ListAsync(
        MatrixTaskQuery query,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var assignedToUserId = actor.Role == MatrixActorRole.TeamLead
            ? actor.UserId
            : query.AssignedToUserId;

        var filter = (query with
        {
            AssignedToUserId = assignedToUserId,
            BranchId = MatrixApplicationService.BranchScope(actor)
        }).ToFilter() with { InvolvingUserId = actor.UserId };

        var page = await uow.MatrixTasks.ListAsync(filter, cancellationToken);
        return page.ToDto(await ResolveCreatorsAsync(page, cancellationToken));
    }

    public async Task<MatrixTaskPage> ListMineAsync(
        MatrixTaskQuery query,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        if (actor.Role != MatrixActorRole.TeamLead)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Chỉ Tổ trưởng mới xem được nhiệm vụ ma trận được giao cho mình.");
        }

        var page = await uow.MatrixTasks.ListAsync(
            (query with { AssignedToUserId = actor.UserId }).ToFilter(),
            cancellationToken);
        return page.ToDto(await ResolveCreatorsAsync(page, cancellationToken));
    }

    public async Task<MatrixReferenceData> GetReferenceDataAsync(
        ulong? academicContextId,
        CancellationToken cancellationToken)
    {
        var model = await uow.MatrixReferences.GetReferenceDataAsync(
            currentUser.Actor,
            academicContextId,
            cancellationToken);
        return model.ToDto();
    }

    public async Task<MatrixTaskResponse> GetAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        var task = await uow.MatrixTasks.GetMatrixTaskAsync(taskId, cancellationToken);
        if (task is null || !string.Equals(task.TaskType, "MATRIX", StringComparison.OrdinalIgnoreCase))
        {
            throw new MatrixApplicationException("NotFound", "Không tìm thấy nhiệm vụ ma trận.");
        }

        if (task.CreatedByUserId != actor.UserId && task.AssignedToUserId != actor.UserId)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Bạn không phụ trách nhiệm vụ này nên không xem được.");
        }

        var matrix = await uow.MatrixTasks.GetTaskMatrixAsync(taskId, cancellationToken);
        EnsureSameBranch(actor, matrix);

        var people = await peopleResolver.ResolveAsync(new ulong?[] { task.CreatedByUserId }, cancellationToken);
        return task.ToResponse(matrix?.Scope.Key ?? 0, matrix is { Started: true } ? matrix.Id : null, people);
    }

    /// <summary>
    /// PHT xoá nhiệm vụ đã giao khi Tổ trưởng chưa bắt đầu. "Đã bắt đầu" = Tổ trưởng đã Lưu nháp
    /// ma trận (có dòng chi tiết) hoặc đã nộp; lúc đó nhiệm vụ không xoá được nữa.
    /// </summary>
    public async Task DeleteAsync(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.Actor;
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Chỉ PHT mới được xóa nhiệm vụ ma trận.");
        }

        var task = await uow.MatrixTasks.GetMatrixTaskAsync(taskId, cancellationToken)
            ?? throw new MatrixApplicationException("NotFound", "Không tìm thấy nhiệm vụ ma trận.");
        if (task.CreatedByUserId != actor.UserId)
            throw new MatrixApplicationException("Forbidden", "Chỉ người giao nhiệm vụ mới được xóa.");
        EnsureSameBranch(actor, await uow.MatrixTasks.GetTaskMatrixAsync(taskId, cancellationToken));

        if (!await uow.MatrixTasks.DeleteIfNotStartedAsync(taskId, cancellationToken))
        {
            // Không xoá được: hoặc người khác vừa xoá, hoặc Tổ trưởng đã có ma trận cho nhiệm vụ này.
            var stillExists = await uow.MatrixTasks.GetMatrixTaskAsync(taskId, cancellationToken) is not null;
            throw stillExists
                ? new MatrixApplicationException("TaskStarted", "Nhiệm vụ đã được thực hiện, không thể xóa.")
                : new MatrixApplicationException("NotFound", "Không tìm thấy nhiệm vụ ma trận.");
        }
    }

    // The task's branch is its matrix's branch.
    private static void EnsureSameBranch(MatrixActor actor, Infrastructure.Models.TaskMatrixRow? matrix)
    {
        var branchScope = MatrixApplicationService.BranchScope(actor);
        if (branchScope is not null && matrix?.Scope.BranchId != branchScope)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Nhiệm vụ ma trận thuộc chi nhánh khác.");
        }
    }

    private Task<IReadOnlyDictionary<ulong, MatrixPerson>> ResolveCreatorsAsync(
        Infrastructure.Models.PagedResult<Infrastructure.Models.MatrixTaskRow> page,
        CancellationToken cancellationToken)
    {
        return peopleResolver.ResolveAsync(
            page.Items.Select(row => (ulong?)row.CreatedByUserId),
            cancellationToken);
    }
}
