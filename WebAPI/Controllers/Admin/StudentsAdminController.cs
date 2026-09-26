using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

[Route("api/admin/schools/{schoolId:min(1)}/students")]
[Authorize(Policy = AdminPolicy)]
public sealed class StudentsAdminController(
    ISchoolDirectoryService readService,
    ISchoolDirectoryAdminService adminService) : SchoolDirectoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        ulong schoolId,
        [FromQuery] StudentListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.ListStudentsAsync(
            DirectoryScope.ForSchool(schoolId), query, cancellationToken));

    [HttpGet("{studentId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.GetStudentAsync(
            DirectoryScope.ForSchool(schoolId), studentId, cancellationToken));

    [HttpGet("{studentId:min(1)}/scores")]
    public async Task<IActionResult> GetScores(
        ulong schoolId,
        ulong studentId,
        [FromQuery] ulong classId,
        [FromQuery] PageQuery page,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.GetStudentScoresAsync(
            DirectoryScope.ForSchool(schoolId), studentId, classId, page, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        ulong schoolId,
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.CreateStudentAsync(
            schoolId, request, cancellationToken));

    [HttpPut("{studentId:min(1)}")]
    public async Task<IActionResult> Update(
        ulong schoolId,
        ulong studentId,
        [FromBody] UpdateStudentRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.UpdateStudentAsync(
            schoolId, studentId, request, cancellationToken));

    // Mid-year class change. The old class stays in the student's history (TRANSFERRED_OUT) and
    // scores keep following the student, so nothing earned in the earlier class is lost.
    [HttpPost("{studentId:min(1)}/transfer-class")]
    public async Task<IActionResult> TransferClass(
        ulong schoolId,
        ulong studentId,
        [FromBody] TransferStudentClassRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.TransferStudentClassAsync(
            schoolId, studentId, request, cancellationToken));

    // Logical delete: the profile turns INACTIVE and keeps its history and exam results.
    [HttpDelete("{studentId:min(1)}")]
    public async Task<IActionResult> Delete(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.DeleteStudentAsync(
            schoolId, studentId, cancellationToken));
}
