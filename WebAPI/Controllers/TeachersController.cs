using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/teachers")]
[Authorize(Policy = "TeacherRead")]
public sealed class TeachersController(ITeacherService service) : ControllerBase
{
    private TeacherScope Scope => new(ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TeacherPage<TeacherListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] TeacherListQuery query, CancellationToken ct) =>
        Ok(ApiResponse<TeacherPage<TeacherListItem>>.Ok(await service.ListAsync(Scope, query, ct)));

    [HttpGet("reference-data")]
    [ProducesResponseType(typeof(ApiResponse<TeacherReferenceData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> References(CancellationToken ct) =>
        Ok(ApiResponse<TeacherReferenceData>.Ok(await service.ReferencesAsync(Scope, ct)));

    [HttpGet("{teacherId:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(ulong teacherId, CancellationToken ct) =>
        Ok(ApiResponse<TeacherDetailDto>.Ok(await service.GetAsync(Scope, teacherId, ct)));
}
