using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/matrix-tasks")]
public sealed class MatrixTasksController(
    IMatrixTaskApplicationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MatrixTaskResponse>> Create(
        CreateMatrixTaskRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { taskId = response.Id }, response);
    }

    [HttpGet]
    public Task<MatrixTaskPage> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] ulong? assignedToUserId = null,
        [FromQuery] DateTime? dueBefore = null,
        [FromQuery] string? keyword = null,
        [FromQuery] ulong? academicContextId = null,
        CancellationToken cancellationToken = default)
    {
        return service.ListAsync(
            new MatrixTaskQuery(page, pageSize, status, assignedToUserId, dueBefore, null, keyword, academicContextId),
            cancellationToken);
    }

    [HttpGet("/api/my/matrix-tasks")]
    public Task<MatrixTaskPage> ListMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dueBefore = null,
        [FromQuery] string? keyword = null,
        [FromQuery] ulong? academicContextId = null,
        CancellationToken cancellationToken = default)
    {
        return service.ListMineAsync(
            new MatrixTaskQuery(page, pageSize, status, null, dueBefore, null, keyword, academicContextId),
            cancellationToken);
    }

    [HttpGet("{taskId}")]
    public Task<MatrixTaskResponse> Get(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return service.GetAsync(taskId, cancellationToken);
    }
}
