using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/classes")]
[Authorize(Policy = SchoolReadPolicy)]
public sealed class ClassesController(ISchoolDirectoryService service) : SchoolDirectoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ClassListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ListClassesAsync(ActorScope, query, cancellationToken));

    [HttpGet("reference-data")]
    public async Task<IActionResult> GetReferenceData(
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.GetReferenceDataAsync(ActorScope, academicYearId, cancellationToken));

    [HttpGet("{classId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong classId,
        [FromQuery] PageQuery rosterPage,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.GetClassAsync(ActorScope, classId, rosterPage, cancellationToken));
}
