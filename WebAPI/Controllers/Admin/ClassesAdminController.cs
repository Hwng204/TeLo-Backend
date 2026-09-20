using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

[Route("api/admin/schools/{schoolId:min(1)}/classes")]
[Authorize(Policy = AdminPolicy)]
public sealed class ClassesAdminController(
    ISchoolDirectoryService readService,
    ISchoolDirectoryAdminService adminService) : SchoolDirectoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        ulong schoolId,
        [FromQuery] ClassListQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.ListClassesAsync(
            DirectoryScope.ForSchool(schoolId), query, cancellationToken));

    [HttpGet("reference-data")]
    public async Task<IActionResult> GetReferenceData(
        ulong schoolId,
        [FromQuery] ulong? academicYearId,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.GetReferenceDataAsync(
            DirectoryScope.ForSchool(schoolId), academicYearId, cancellationToken));

    [HttpGet("{classId:min(1)}")]
    public async Task<IActionResult> GetById(
        ulong schoolId,
        ulong classId,
        [FromQuery] PageQuery rosterPage,
        CancellationToken cancellationToken) =>
        ToActionResult(await readService.GetClassAsync(
            DirectoryScope.ForSchool(schoolId), classId, rosterPage, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        ulong schoolId,
        [FromBody] CreateClassRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.CreateClassAsync(schoolId, request, cancellationToken));

    [HttpPut("{classId:min(1)}")]
    public async Task<IActionResult> Update(
        ulong schoolId,
        ulong classId,
        [FromBody] UpdateClassRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.UpdateClassAsync(
            schoolId, classId, request, cancellationToken));

    // Logical delete: the class turns INACTIVE so history and scores survive.
    [HttpDelete("{classId:min(1)}")]
    public async Task<IActionResult> Delete(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.DeleteClassAsync(schoolId, classId, cancellationToken));
}
