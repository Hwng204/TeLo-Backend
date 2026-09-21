using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/schools/{schoolId:min(1)}/branches/{branchId:min(1)}/teachers")]
[Authorize(Policy = SchoolDirectoryControllerBase.AdminPolicy)]
public sealed class TeachersAdminController(ITeacherService service) : ControllerBase
{
    private ulong ActorId => ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private TeacherScope Scope(ulong schoolId, ulong branchId) => new(ActorId, schoolId, branchId);

    [HttpGet("/api/admin/teacher-schools")]
    [ProducesResponseType(typeof(ApiResponse<TeacherPage<TeacherOption>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schools([FromQuery] TeacherSelectionQuery query, CancellationToken ct) =>
        Ok(ApiResponse<TeacherPage<TeacherOption>>.Ok(await service.SchoolsAsync(ActorId, query, ct)));

    [HttpGet("/api/admin/schools/{schoolId:min(1)}/teacher-branches")]
    [ProducesResponseType(typeof(ApiResponse<TeacherPage<TeacherOption>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Branches(ulong schoolId, [FromQuery] TeacherSelectionQuery query, CancellationToken ct) =>
        Ok(ApiResponse<TeacherPage<TeacherOption>>.Ok(await service.BranchesAsync(ActorId, schoolId, query, ct)));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TeacherPage<TeacherListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(ulong schoolId, ulong branchId, [FromQuery] TeacherListQuery query, CancellationToken ct) =>
        Ok(ApiResponse<TeacherPage<TeacherListItem>>.Ok(await service.ListAsync(Scope(schoolId, branchId), query, ct)));

    [HttpGet("reference-data")]
    [ProducesResponseType(typeof(ApiResponse<TeacherReferenceData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> References(ulong schoolId, ulong branchId, CancellationToken ct) =>
        Ok(ApiResponse<TeacherReferenceData>.Ok(await service.ReferencesAsync(Scope(schoolId, branchId), ct)));

    [HttpGet("{teacherId:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(ulong schoolId, ulong branchId, ulong teacherId, CancellationToken ct) =>
        Ok(ApiResponse<TeacherDetailDto>.Ok(await service.GetAsync(Scope(schoolId, branchId), teacherId, ct)));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ulong schoolId, ulong branchId, CreateTeacherRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(Scope(schoolId, branchId), request, ct);
        return CreatedAtAction(nameof(Get), new { schoolId, branchId, teacherId = result.Teacher.Id },
            ApiResponse<TeacherDetailDto>.Ok(result));
    }

    [HttpPut("{teacherId:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(ulong schoolId, ulong branchId, ulong teacherId, UpdateTeacherRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TeacherDetailDto>.Ok(await service.UpdateAsync(Scope(schoolId, branchId), teacherId, request, ct)));

    // Logical deletion retains class/exam history. Version is obtained from the most recent GET.
    [HttpDelete("{teacherId:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TeacherDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(ulong schoolId, ulong branchId, ulong teacherId, [FromQuery] uint version, CancellationToken ct) =>
        Ok(ApiResponse<TeacherDetailDto>.Ok(await service.DeleteAsync(Scope(schoolId, branchId), teacherId, version, ct)));

    [HttpGet("{teacherId:min(1)}/account")]
    [ProducesResponseType(typeof(ApiResponse<TeacherAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Account(ulong schoolId, ulong branchId, ulong teacherId, CancellationToken ct) =>
        Ok(ApiResponse<TeacherAccountDto>.Ok(await service.AccountAsync(Scope(schoolId, branchId), teacherId, ct)));

    [HttpPatch("{teacherId:min(1)}/account")]
    [ProducesResponseType(typeof(ApiResponse<TeacherAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAccount(ulong schoolId, ulong branchId, ulong teacherId, UpdateTeacherAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TeacherAccountDto>.Ok(await service.UpdateAccountAsync(Scope(schoolId, branchId), teacherId, request, ct)));

    [HttpPost("{teacherId:min(1)}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse<TeacherAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(ulong schoolId, ulong branchId, ulong teacherId, ResetTeacherPasswordRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TeacherAccountDto>.Ok(await service.ResetPasswordAsync(Scope(schoolId, branchId), teacherId, request, ct)));
}
