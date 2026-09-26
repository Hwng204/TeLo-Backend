using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

// School side: a principal or vice principal uploads a student list, checks the preview and
// submits it. Nothing is written to the student tables until an admin approves the batch.
[Route("api/student-imports")]
[Authorize(Policy = ImportPolicy)]
public sealed class StudentImportsController(IStudentImportService service)
    : StudentImportControllerBase
{
    private ImportCaller Caller => ImportCaller.School(ActorUserId);

    // "View template": column definitions plus the class codes valid for the chosen year.
    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate(
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetTemplateInfoAsync(Caller, academicYearId, cancellationToken));

    // "Download template".
    [HttpGet("template.xlsx")]
    public async Task<IActionResult> DownloadTemplate(
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToFileResult(await service.BuildTemplateAsync(Caller, academicYearId, cancellationToken));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public Task<IActionResult> Upload(
        IFormFile? file,
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        UploadAsync(service, Caller, file, academicYearId, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] StudentImportListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ListAsync(Caller, query, cancellationToken));

    [HttpGet("{batchId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong batchId,
        [FromQuery] StudentImportRowsQuery rows,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetAsync(Caller, batchId, rows, cancellationToken));

    [HttpGet("{batchId:min(1)}/file.xlsx")]
    public async Task<IActionResult> DownloadFile(
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToFileResult(await service.GetFileAsync(Caller, batchId, cancellationToken));

    [HttpPost("{batchId:min(1)}/submit")]
    public async Task<IActionResult> Submit(
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.SubmitAsync(Caller, batchId, cancellationToken));

    [HttpDelete("{batchId:min(1)}")]
    public async Task<IActionResult> Cancel(
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.CancelAsync(Caller, batchId, cancellationToken));
}
