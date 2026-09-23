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
    IMatrixPeopleResolver peopleResolver) : IMatrixTaskApplicationService
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
                "Người nhận, ngữ cảnh học thuật và tên nhiệm vụ là bắt buộc.");
        }

        if (request.Name.Trim().Length > 255)
        {
            throw new MatrixApplicationException(
                "InvalidRequest",
                "Tên nhiệm vụ tối đa 255 ký tự.");
        }

        var actor = currentUser.Actor;
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Chỉ PHT mới được giao nhiệm vụ ma trận.");
        }

        await uow.MatrixReferences.EnsureAssignmentValidAsync(
            actor,
            request.AssignedToUserId,
            request.AcademicContextId,
            request.SemesterId,
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
                AcademicContextId = request.AcademicContextId,
                SemesterId = request.SemesterId,
                CreatedAt = DateTime.UtcNow
            };

            await uow.MatrixTasks.AddAsync(task, ct);
            await uow.CompleteAsync(ct);
            return task.ToResponse(null, await peopleResolver.ResolveAsync(new ulong?[] { task.CreatedByUserId }, ct));
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
            BranchId = BranchScope(actor)
        }).ToFilter();

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

        if (actor.Role == MatrixActorRole.TeamLead && task.AssignedToUserId != actor.UserId)
        {
            throw new MatrixApplicationException(
                "Forbidden",
                "Bạn không phải Tổ trưởng được giao nhiệm vụ này.");
        }

        await EnsureSameBranchAsync(actor, task, cancellationToken);

        var matrixId = await uow.MatrixTasks.GetLinkedMatrixIdAsync(taskId, cancellationToken);
        var people = await peopleResolver.ResolveAsync(new ulong?[] { task.CreatedByUserId }, cancellationToken);
        return task.ToResponse(matrixId, people);
    }

    /// <summary>
    /// PHT xoá nhiệm vụ đã giao khi Tổ trưởng chưa bắt đầu. "Đã bắt đầu" = đã có ma trận
    /// (Tổ trưởng đã Lưu nháp hoặc đã nộp); lúc đó nhiệm vụ không xoá được nữa.
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
                "Chỉ PHT mới được xoá nhiệm vụ ma trận.");
        }

        var task = await uow.MatrixTasks.GetMatrixTaskAsync(taskId, cancellationToken)
            ?? throw new MatrixApplicationException("NotFound", "Không tìm thấy nhiệm vụ ma trận.");
        await EnsureSameBranchAsync(actor, task, cancellationToken);

        if (!await uow.MatrixTasks.DeleteIfNotStartedAsync(taskId, cancellationToken))
        {
            // Không xoá được: hoặc người khác vừa xoá, hoặc Tổ trưởng đã có ma trận cho nhiệm vụ này.
            var stillExists = await uow.MatrixTasks.GetMatrixTaskAsync(taskId, cancellationToken) is not null;
            throw stillExists
                ? new MatrixApplicationException("TaskStarted", "Nhiệm vụ đã được thực hiện, không thể xóa.")
                : new MatrixApplicationException("NotFound", "Không tìm thấy nhiệm vụ ma trận.");
        }
    }

    private async Task EnsureSameBranchAsync(
        MatrixActor actor,
        WorkTask task,
        CancellationToken cancellationToken)
    {
        var branchScope = BranchScope(actor);
        if (branchScope is not null &&
            (task.AcademicContextId is null ||
             await uow.MatrixTasks.GetContextBranchIdAsync(task.AcademicContextId.Value, cancellationToken) != branchScope))
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
}
