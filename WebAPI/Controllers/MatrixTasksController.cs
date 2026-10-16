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

    [HttpGet("{taskId}")]
    public Task<MatrixTaskResponse> Get(
        ulong taskId,
        CancellationToken cancellationToken)
    {
        return service.GetAsync(taskId, cancellationToken);
    }
}
