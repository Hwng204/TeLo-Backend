using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

// Admin side: import straight into a school named in the route, and review what schools sent.
[Route("api/admin/schools/{schoolId:min(1)}/student-imports")]
[Authorize(Policy = AdminPolicy)]
public sealed class StudentImportsAdminController(IStudentImportService service)
    : StudentImportControllerBase
{
    private ImportCaller Caller(ulong schoolId) => ImportCaller.Admin(ActorUserId, schoolId);

    // Review inbox across every school, e.g. ?status=SUBMITTED. Act on a batch through the
    // school-scoped routes below using the schoolId each item carries.
    [HttpGet("~/api/admin/student-imports")]
    public async Task<IActionResult> ListAllSchools(
        [FromQuery] StudentImportListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ListAsync(
            ImportCaller.Admin(ActorUserId, null), query, cancellationToken));

    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate(
        ulong schoolId,
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetTemplateInfoAsync(
            Caller(schoolId), academicYearId, cancellationToken));

    [HttpGet("template.xlsx")]
    public async Task<IActionResult> DownloadTemplate(
        ulong schoolId,
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToFileResult(await service.BuildTemplateAsync(
            Caller(schoolId), academicYearId, cancellationToken));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public Task<IActionResult> Upload(
        ulong schoolId,
        IFormFile? file,
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        UploadAsync(service, Caller(schoolId), file, academicYearId, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> List(
        ulong schoolId,
        [FromQuery] StudentImportListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ListAsync(Caller(schoolId), query, cancellationToken));

    [HttpGet("{batchId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong schoolId,
        ulong batchId,
        [FromQuery] StudentImportRowsQuery rows,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetAsync(Caller(schoolId), batchId, rows, cancellationToken));

    [HttpGet("{batchId:min(1)}/file.xlsx")]
    public async Task<IActionResult> DownloadFile(
        ulong schoolId,
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToFileResult(await service.GetFileAsync(Caller(schoolId), batchId, cancellationToken));

    // Writes the students: an admin batch straight from DRAFT, a school batch once SUBMITTED.
    [HttpPost("{batchId:min(1)}/apply")]
    public async Task<IActionResult> Apply(
        ulong schoolId,
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ApplyAsync(Caller(schoolId), batchId, cancellationToken));

    [HttpPost("{batchId:min(1)}/reject")]
    public async Task<IActionResult> Reject(
        ulong schoolId,
        ulong batchId,
        [FromBody] RejectStudentImportRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.RejectAsync(
            Caller(schoolId), batchId, request, cancellationToken));

    [HttpDelete("{batchId:min(1)}")]
    public async Task<IActionResult> Cancel(
        ulong schoolId,
        ulong batchId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.CancelAsync(Caller(schoolId), batchId, cancellationToken));
}
