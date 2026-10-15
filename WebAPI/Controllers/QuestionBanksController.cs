using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = TasksController.ReadPolicy)]
[Route("api/question-banks")]
public sealed class QuestionBanksController(QuestionBankService service, IMatrixRoleCatalog roles, IConfiguration configuration)
    : QuestionControllerBase(roles, configuration)
{
    [HttpGet("questions")]
    public Task<IActionResult> List([FromQuery] QuestionBankQuery query, CancellationToken ct) => Result(() => service.ListAsync(Actor(), query, ct));
    [HttpGet("questions/{id:min(1)}")]
    public Task<IActionResult> Detail(ulong id, CancellationToken ct) => Result(() => service.DetailAsync(Actor(), id, ct));
    [HttpGet("options")]
    public Task<IActionResult> Options(CancellationToken ct) => Result(() => service.OptionsAsync(Actor(), ct));
    [HttpGet("matrices/{id:min(1)}/availability")]
    public Task<IActionResult> Availability(ulong id, CancellationToken ct) => Result(() => service.AvailabilityAsync(Actor(), id, ct));
    [HttpPost("questions/{id:min(1)}/move-to-practice")]
    public Task<IActionResult> Move(ulong id, CancellationToken ct) => Result(() => service.MoveAsync(Actor(), id, ct), "Đã chuyển câu hỏi sang ôn tập.");
    [HttpGet("overdue-count")]
    public Task<IActionResult> Overdue(CancellationToken ct) => Result(() => service.OverdueAsync(Actor(), ct));
}
