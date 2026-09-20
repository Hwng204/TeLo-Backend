using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "OperationalAdmin")]
[Route("api/exams")]
public sealed class ExamsController(IExamService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                ApiResponse<ExamDetailDto>.Ok(result.Value, "Đã tạo kỳ thi."));
        }

        return ToErrorResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ExamPage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamPage>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword = null,
        [FromQuery] ulong? semesterId = null,
        [FromQuery] ulong? schoolBranchId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(
            new ExamListQuery(
                keyword,
                semesterId,
                schoolBranchId,
                status,
                fromDate,
                toDate,
                pageNumber,
                pageSize,
                sortBy,
                sortDirection),
            cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamPage>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamDetailDto>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamDetailDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] ulong id,
        [FromBody] UpdateExamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamDetailDto>.Ok(result.Value, "Đã cập nhật kỳ thi."))
            : ToErrorResult(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Đã xóa kỳ thi."))
            : ToErrorResult(result);
    }

    private IActionResult ToErrorResult<T>(ServiceResult<T> result)
    {
        var error = result.Error!;
        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "EXAM_NOT_FOUND" or "SEMESTER_NOT_FOUND" or "SCHOOL_BRANCH_NOT_FOUND" => NotFound(response),
            "EXAM_HAS_DEPENDENCIES" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
