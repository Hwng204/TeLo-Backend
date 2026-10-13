using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = TasksController.ReadPolicy)]
[Route("api/tasks/{taskId:min(1)}/papers/{paperId:min(1)}/questions")]
public sealed class QuestionPapersController(QuestionPaperService service, IMatrixRoleCatalog roles, IConfiguration configuration) : QuestionControllerBase(roles, configuration)
{
    [HttpGet]
    public Task<IActionResult> Get(ulong taskId, ulong paperId, CancellationToken ct) => Result(() => service.GetAsync(Actor(), taskId, paperId, ct));
    [HttpPost]
    public Task<IActionResult> Add(ulong taskId, ulong paperId, PaperQuestionRequest request, CancellationToken ct) => Result(() => service.AddAsync(Actor(), taskId, paperId, request, ct));
    [HttpDelete("{questionId:min(1)}")]
    public Task<IActionResult> Remove(ulong taskId, ulong paperId, ulong questionId, [FromQuery] int version, CancellationToken ct) => Result(() => service.RemoveAsync(Actor(), taskId, paperId, questionId, version, ct));
}
