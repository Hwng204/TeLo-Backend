using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/academic-years")]
public sealed class AcademicYearsController(IAcademicYearService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearPage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Trang phải lớn hơn hoặc bằng 1."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Kích thước trang phải từ 1 đến 100."];
        if (status is not null && status is not ("DRAFT" or "ACTIVE" or "CLOSED"))
        {
            errors["status"] = ["Trạng thái phải là DRAFT, ACTIVE hoặc CLOSED."];
        }

        if (errors.Count > 0)
        {
            return UnprocessableEntity(ApiResponse<AcademicYearPage>.Fail(
                "VALIDATION_ERROR",
                "Tham số truy vấn không hợp lệ.",
                errors));
        }

        var result = await service.ListAsync(
            new AcademicYearListQuery(status, search, page, pageSize),
            cancellationToken);
        return Ok(ApiResponse<AcademicYearPage>.Ok(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearListItem>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearListItem>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearListItem>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAcademicYearRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Created(
                $"/api/academic-years/{result.Value.Id}",
                ApiResponse<AcademicYearListItem>.Ok(result.Value, "Đã tạo năm học."));
        }

        var error = result.Error!;
        var response = ApiResponse<AcademicYearListItem>.Fail(error.Code, error.Message, error.Details);

        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "ACADEMIC_YEAR_CONFLICT" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] ulong id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<AcademicYearDetailDto>.Ok(result.Value));
        }

        var error = result.Error!;
        return NotFound(ApiResponse<AcademicYearDetailDto>.Fail(error.Code, error.Message, error.Details));
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] ulong id,
        [FromBody] UpdateAcademicYearRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<AcademicYearDetailDto>.Ok(result.Value, "Đã cập nhật năm học."));
        }

        var error = result.Error!;
        var response = ApiResponse<AcademicYearDetailDto>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "ACADEMIC_YEAR_NOT_FOUND" => NotFound(response),
            "ACADEMIC_YEAR_CLOSED" or "ACADEMIC_YEAR_CONFLICT" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:long}/activate")]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate([FromRoute] ulong id, CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(id, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<AcademicYearDetailDto>.Ok(result.Value, "Đã kích hoạt năm học."));
        }

        var error = result.Error!;
        var response = ApiResponse<AcademicYearDetailDto>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "ACADEMIC_YEAR_NOT_FOUND" => NotFound(response),
            "ACADEMIC_YEAR_ALREADY_ACTIVE" or "ACADEMIC_YEAR_CLOSED" or "INCOMPLETE_TERMS" or "ACTIVE_YEAR_CONFLICT" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:long}/close")]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close([FromRoute] ulong id, CancellationToken cancellationToken)
    {
        var result = await service.CloseAsync(id, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<AcademicYearDetailDto>.Ok(result.Value, "Đã đóng năm học."));
        }

        var error = result.Error!;
        var response = ApiResponse<AcademicYearDetailDto>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "ACADEMIC_YEAR_NOT_FOUND" => NotFound(response),
            "ACADEMIC_YEAR_ALREADY_CLOSED" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPut("{id:long}/terms")]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDetailDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfigureTerms(
        [FromRoute] ulong id,
        [FromBody] ConfigureTermsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfigureTermsAsync(id, request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<AcademicYearDetailDto>.Ok(result.Value, "Đã cấu hình học kỳ."));
        }

        var error = result.Error!;
        var response = ApiResponse<AcademicYearDetailDto>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "ACADEMIC_YEAR_NOT_FOUND" => NotFound(response),
            "ACADEMIC_YEAR_CLOSED" or "TERM_CLOSED" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{yearId:long}/terms/{termId:long}/close")]
    [ProducesResponseType(typeof(ApiResponse<SemesterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SemesterDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<SemesterDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseTerm(
        [FromRoute] ulong yearId,
        [FromRoute] ulong termId,
        CancellationToken cancellationToken)
    {
        var result = await service.CloseTermAsync(yearId, termId, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(ApiResponse<SemesterDto>.Ok(result.Value, "Đã đóng học kỳ."));
        }

        var error = result.Error!;
        var response = ApiResponse<SemesterDto>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "ACADEMIC_YEAR_NOT_FOUND" or "TERM_NOT_FOUND" => NotFound(response),
            "TERM_ALREADY_CLOSED" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
