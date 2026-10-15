using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = TasksController.ReadPolicy)]
[Route("api/tasks/{taskId:min(1)}")]
public sealed class QuestionsController(QuestionService service, IMatrixRoleCatalog roles, IConfiguration configuration)
    : QuestionControllerBase(roles, configuration)
{
    [HttpGet("question-package")]
    public Task<IActionResult> Get(ulong taskId, CancellationToken ct) => Result(() => service.GetAsync(Actor(), taskId, ct));
    [HttpPost("questions")]
    public Task<IActionResult> Create(ulong taskId, SaveQuestionRequest request, CancellationToken ct) => Result(() => service.SaveAsync(Actor(), taskId, null, request, ct), "Đã lưu câu hỏi.");
    [HttpPut("questions/{id:min(1)}")]
    public Task<IActionResult> Update(ulong taskId, ulong id, SaveQuestionRequest request, CancellationToken ct) => Result(() => service.SaveAsync(Actor(), taskId, id, request, ct), "Đã lưu câu hỏi.");
    [HttpDelete("questions/{id:min(1)}")]
    public Task<IActionResult> Delete(ulong taskId, ulong id, [FromQuery] int version, CancellationToken ct) => Result(() => service.DeleteAsync(Actor(), taskId, id, version, ct), "Đã xóa câu hỏi.");
    [HttpPost("question-package/submit")]
    public Task<IActionResult> Submit(ulong taskId, QuestionCommand request, CancellationToken ct) => Result(() => service.SubmitAsync(Actor(), taskId, request.Version, ct), "Đã gửi gói câu hỏi.");
    [HttpPost("question-package/approve")]
    public Task<IActionResult> Approve(ulong taskId, ReviewQuestionRequest request, CancellationToken ct) => Result(() => service.ReviewAsync(Actor(), taskId, true, request, ct), "Đã chốt gói câu hỏi.");
    [HttpPost("question-package/reject")]
    public Task<IActionResult> Reject(ulong taskId, ReviewQuestionRequest request, CancellationToken ct) => Result(() => service.ReviewAsync(Actor(), taskId, false, request, ct), "Đã yêu cầu chỉnh sửa.");
}
