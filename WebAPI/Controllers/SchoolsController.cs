using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/schools")]
public sealed class SchoolsController(ISchoolService service) : ControllerBase
{
    /// <summary>Danh sách tất cả trường, có thể lọc theo từ khoá.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SchoolListPage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(search, cancellationToken);
        return Ok(ApiResponse<SchoolListPage>.Ok(result));
    }

    /// <summary>Chi tiết một trường kèm danh sách cơ sở.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
            return Ok(ApiResponse<SchoolDetailDto>.Ok(result.Value));

        var error = result.Error!;
        return NotFound(ApiResponse<SchoolDetailDto>.Fail(error.Code, error.Message));
    }

    /// <summary>Tạo trường mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSchoolRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
            return UnprocessableEntity(ApiResponse<SchoolListItem>.Fail(
                "VALIDATION_ERROR", "Dữ liệu trường không hợp lệ.", errors));

        var result = await service.CreateAsync(request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
            return Created($"/api/schools/{result.Value.Id}",
                ApiResponse<SchoolListItem>.Ok(result.Value, "Đã tạo trường thành công."));

        var error = result.Error!;
        var response = ApiResponse<SchoolListItem>.Fail(error.Code, error.Message);
        return error.Code switch
        {
            "SCHOOL_CODE_CONFLICT" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Cập nhật thông tin trường (không thay đổi mã).</summary>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<SchoolListItem>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] ulong id,
        [FromBody] UpdateSchoolRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateUpdateRequest(request);
        if (errors.Count > 0)
            return UnprocessableEntity(ApiResponse<SchoolListItem>.Fail(
                "VALIDATION_ERROR", "Dữ liệu cập nhật không hợp lệ.", errors));

        var result = await service.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
            return Ok(ApiResponse<SchoolListItem>.Ok(result.Value, "Đã cập nhật trường thành công."));

        var error = result.Error!;
        return error.Code switch
        {
            "SCHOOL_NOT_FOUND" => NotFound(ApiResponse<SchoolListItem>.Fail(error.Code, error.Message)),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // ─── Validation ───────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, string[]> ValidateCreateRequest(CreateSchoolRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name))
            errors["name"] = ["Tên trường không được để trống."];
        if (string.IsNullOrWhiteSpace(req.Code))
            errors["code"] = ["Mã trường không được để trống."];
        else if (!System.Text.RegularExpressions.Regex.IsMatch(req.Code.Trim(), @"^[A-Za-z0-9\-_]+$"))
            errors["code"] = ["Mã chỉ gồm chữ cái, số và dấu gạch ngang."];
        return errors;
    }

    private static IReadOnlyDictionary<string, string[]> ValidateUpdateRequest(UpdateSchoolRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Name))
            errors["name"] = ["Tên trường không được để trống."];
        return errors;
    }
}
