using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = TasksController.ReadPolicy)]
[Route("api/exam-packages")]
public sealed class ExamPackagesController(ExamPackageService service, IMatrixRoleCatalog roles, IConfiguration configuration) : QuestionControllerBase(roles, configuration)
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] ExamPackageQuery query, CancellationToken ct) => Run(() => service.ListAsync(Actor(), query, ct));
    [HttpGet("options")]
    public Task<IActionResult> Options([FromQuery] ulong? packageId, CancellationToken ct) => Run(() => service.OptionsAsync(Actor(), ct, packageId));
    [HttpPost]
    public Task<IActionResult> Generate(GenerateExamPackageRequest request, CancellationToken ct) => Run(() => service.GenerateAsync(Actor(), request, ct));
    [HttpGet("{id:min(1)}")]
    public Task<IActionResult> Get(ulong id, CancellationToken ct) => Run(() => service.GetAsync(Actor(), id, ct));
    [HttpPut("{id:min(1)}")]
    public Task<IActionResult> Rename(ulong id, RenameExamPackageRequest request, CancellationToken ct) => Run(() => service.RenameAsync(Actor(), id, request, ct));
    [HttpPost("{id:min(1)}/review-assignment")]
    public Task<IActionResult> Assign(ulong id, AssignExamReviewRequest request, CancellationToken ct) => Run(() => service.AssignAsync(Actor(), id, request, ct));
    [HttpPut("{id:min(1)}/questions/{rowId:min(1)}")]
    public Task<IActionResult> Replace(ulong id, ulong rowId, ReplaceExamQuestionRequest request, CancellationToken ct) => Run(() => service.ReplaceAsync(Actor(), id, rowId, request, ct));
    [HttpPost("{id:min(1)}/submit-review")]
    public Task<IActionResult> Submit(ulong id, ExamPackageCommand request, CancellationToken ct) => Run(() => service.SubmitAsync(Actor(), id, request, ct));
    [HttpPost("{id:min(1)}/return-review")]
    public Task<IActionResult> Return(ulong id, ExamPackageCommand request, CancellationToken ct) => Run(() => service.ReturnAsync(Actor(), id, request, ct));
    [HttpPost("{id:min(1)}/finalize")]
    public Task<IActionResult> Finalize(ulong id, ExamPackageCommand request, CancellationToken ct) => Run(() => service.FinalizeAsync(Actor(), id, request, ct));
    [HttpPost("{id:min(1)}/practice-copy")]
    public Task<IActionResult> Practice(ulong id, ExamPackageCommand request, CancellationToken ct) => Run(() => service.PracticeAsync(Actor(), id, request, ct));
    [HttpDelete("{id:min(1)}")]
    public Task<IActionResult> Delete(ulong id, [FromQuery, BindRequired] int version, CancellationToken ct) => Run(() => service.DeleteAsync(Actor(), id, version, ct));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return await Result(action); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse<T>.Fail("EXAM_PACKAGE_CONFLICT", "Đề đã thay đổi. Vui lòng tải lại.")); }
    }
}
