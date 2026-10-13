using Application.Common;
using Application.Services.Implement;
using Infrastructure.Exports;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = TasksController.ReadPolicy)]
[Route("api/tasks/{taskId:min(1)}/questions/import")]
public sealed class QuestionImportsController(QuestionImportService service, IMatrixRoleCatalog roles, IConfiguration configuration) : QuestionControllerBase(roles, configuration)
{
    [HttpGet("template.xlsx")]
    public async Task<IActionResult> Template(ulong taskId, CancellationToken ct)
    {
        try { return File(await service.TemplateAsync(Actor(), taskId, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau-cau-hoi.xlsx"); }
        catch (QuestionException ex) { return StatusCode(ex.Status, ApiResponse<object>.Fail(ex.Code, ex.Message)); }
    }
    [HttpPost("preview")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> Preview(ulong taskId, [FromQuery] int version, IFormFile? file, CancellationToken ct) => Read(taskId, version, file, false, ct);
    [HttpGet("guide.xlsx")]
    public async Task<IActionResult> Guide(ulong taskId, CancellationToken ct)
    {
        try { return File(await service.GuideAsync(Actor(), taskId, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Huong-dan-va-vi-du-cau-hoi.xlsx"); }
        catch (QuestionException ex) { return StatusCode(ex.Status, ApiResponse<object>.Fail(ex.Code, ex.Message)); }
    }
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> Import(ulong taskId, [FromQuery] int version, IFormFile? file, CancellationToken ct) => Read(taskId, version, file, true, ct);
    private Task<IActionResult> Read(ulong taskId, int version, IFormFile? file, bool confirm, CancellationToken ct) => Result(async () =>
    {
        if (file is null || file.Length is 0 or > QuestionImportWorkbook.MaxBytes) throw new QuestionException("IMPORT_INVALID", "Chọn tệp Excel .xlsx không rỗng, tối đa 5 MB.");
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct);
        return await service.ReadAsync(Actor(), taskId, version, file.FileName, stream.ToArray(), confirm, ct);
    });
}
