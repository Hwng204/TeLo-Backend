using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

// PHT giao ma trận; nhiệm vụ review đề được tạo sau khi PHT sinh đề tự động.
// Tổ trưởng giao câu hỏi; giáo viên/tổ trưởng được giao review đề đã có câu.
// Vai trò cụ thể (ai được giao, sửa, xoá) do TaskService kiểm tra; phân hiệu lấy từ tài khoản.
[ApiController]
[Route("api/tasks")]
[Authorize(Policy = ReadPolicy)]
public sealed class TasksController(ITaskService service, IMatrixRoleCatalog roleCatalog) : ControllerBase
{
    public const string ReadPolicy = "TaskRead";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TaskListQuery query, CancellationToken cancellationToken) =>
        ToActionResult(await service.ListAsync(Actor(), query, cancellationToken));

    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken cancellationToken) =>
        ToActionResult(await service.GetOptionsAsync(Actor(), cancellationToken));

    [HttpGet("{taskId:min(1)}")]
    public async Task<IActionResult> Get(ulong taskId, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetAsync(Actor(), taskId, cancellationToken));

    [HttpDelete("{taskId:min(1)}")]
    public async Task<IActionResult> Delete(ulong taskId, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(Actor(), taskId, cancellationToken);
        return ToActionResult(result, result.Value is { } name ? $"Đã xóa nhiệm vụ \"{name}\"." : null);
    }

    [HttpPut("matrix/{taskId:min(1)}")]
    public async Task<IActionResult> UpdateMatrix(
        ulong taskId,
        [FromBody] CreateMatrixTaskRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.UpdateMatrixAsync(Actor(), taskId, request, cancellationToken), "Đã lưu thay đổi nhiệm vụ.");

    [HttpPost("exam")]
    public async Task<IActionResult> CreateExam([FromBody] SaveExamTaskRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.CreateExamAsync(Actor(), request, cancellationToken));

    [HttpPut("exam/{taskId:min(1)}")]
    public async Task<IActionResult> UpdateExam(
        ulong taskId,
        [FromBody] SaveExamTaskRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.UpdateExamAsync(Actor(), taskId, request, cancellationToken), "Đã lưu thay đổi nhiệm vụ.");

    [HttpPost("question")]
    public async Task<IActionResult> CreateQuestion([FromBody] SaveQuestionTaskRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.CreateQuestionAsync(Actor(), request, cancellationToken), "Đã giao nhiệm vụ biên soạn câu hỏi.");

    [HttpPut("question/{taskId:min(1)}")]
    public async Task<IActionResult> UpdateQuestion(
        ulong taskId,
        [FromBody] SaveQuestionTaskRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.UpdateQuestionAsync(Actor(), taskId, request, cancellationToken), "Đã lưu thay đổi nhiệm vụ.");

    private TaskActor Actor()
    {
        var userId = ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        return new TaskActor(userId, roles.Any(roleCatalog.IsPht), roles.Any(roleCatalog.IsTeamLead), roles.Any(roleCatalog.IsPrincipal));
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result, string? successMessage = null)
    {
        if (result.Error is not { } error)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!, successMessage));
        }

        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            TaskErrorCodes.Validation => UnprocessableEntity(response),
            TaskErrorCodes.BranchRequired or TaskErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            TaskErrorCodes.NotFound => NotFound(response),
            TaskErrorCodes.Started or TaskErrorCodes.Completed or TaskErrorCodes.ExamFlowChanged => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
