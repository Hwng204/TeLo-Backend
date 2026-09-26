using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/matrices")]
public sealed class MatricesController(
    IMatrixApplicationService service) : ControllerBase
{
    [HttpGet]
    public Task<MatrixPage> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] ulong? academicContextId = null,
        [FromQuery] ulong? semesterId = null,
        [FromQuery] string? status = null,
        [FromQuery] ulong? academicYearId = null,
        [FromQuery] ulong? subjectId = null,
        [FromQuery] ulong? gradeLevelId = null,
        CancellationToken cancellationToken = default)
    {
        return service.ListAsync(
            new MatrixListQuery(
                page,
                pageSize,
                keyword,
                academicContextId,
                semesterId,
                status,
                AcademicYearId: academicYearId,
                SubjectId: subjectId,
                GradeLevelId: gradeLevelId),
            cancellationToken);
    }

    [HttpGet("{matrixId}")]
    public Task<MatrixResponse> Get(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        return service.GetAsync(matrixId, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<MatrixResponse>> Create(
        SaveMatrixRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matrixId = response.Id }, response);
    }

    [HttpPut("{matrixId}")]
    public Task<MatrixResponse> Update(
        ulong matrixId,
        SaveMatrixRequest request,
        CancellationToken cancellationToken)
    {
        return service.UpdateAsync(matrixId, request, cancellationToken);
    }

    [HttpDelete("{matrixId}")]
    public async Task<IActionResult> Delete(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        await service.DeleteDraftAsync(matrixId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{matrixId}/submit")]
    public Task<MatrixResponse> Submit(ulong matrixId, CancellationToken cancellationToken) =>
        service.SubmitAsync(matrixId, cancellationToken);

    [HttpPost("{matrixId}/reject")]
    public Task<MatrixResponse> Reject(
        ulong matrixId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectMatrixRequest? request,
        CancellationToken cancellationToken) =>
        service.RejectAsync(matrixId, request, cancellationToken);

    [HttpPost("{matrixId}/approve")]
    public Task<MatrixResponse> Approve(ulong matrixId, CancellationToken cancellationToken) =>
        service.ApproveAsync(matrixId, cancellationToken);

    [HttpPost("{matrixId}/confirm")]
    public Task<MatrixResponse> Confirm(ulong matrixId, CancellationToken cancellationToken) =>
        service.ConfirmDirectAsync(matrixId, cancellationToken);

    [HttpPost("{matrixId}/archive")]
    public Task<MatrixResponse> Archive(ulong matrixId, CancellationToken cancellationToken) =>
        service.ArchiveAsync(matrixId, cancellationToken);

    [HttpPost("{matrixId}/clone")]
    public async Task<ActionResult<MatrixResponse>> Clone(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var response = await service.CloneAsync(matrixId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { matrixId = response.Id }, response);
    }

    [HttpGet("{matrixId}/export.xlsx")]
    public async Task<IActionResult> Export(
        ulong matrixId,
        CancellationToken cancellationToken)
    {
        var export = await service.ExportAsync(matrixId, cancellationToken);
        return File(
            export.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            export.FileName);
    }
}
