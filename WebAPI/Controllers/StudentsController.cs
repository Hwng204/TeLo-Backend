using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/students")]
[Authorize(Policy = SchoolReadPolicy)]
public sealed class StudentsController(ISchoolDirectoryService service) : SchoolDirectoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] StudentListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.ListStudentsAsync(ActorScope, query, cancellationToken));

    [HttpGet("{studentId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong studentId,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetStudentAsync(ActorScope, studentId, cancellationToken));

    [HttpGet("{studentId:min(1)}/scores")]
    public async Task<IActionResult> GetScores(
        ulong studentId,
        [FromQuery] ulong classId,
        [FromQuery] PageQuery page,
        CancellationToken cancellationToken) =>
        ToActionResult(await service.GetStudentScoresAsync(
            ActorScope, studentId, classId, page, cancellationToken));
}
