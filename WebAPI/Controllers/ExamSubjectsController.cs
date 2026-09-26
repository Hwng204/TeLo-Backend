using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "ExamSubjectManager")]
[Route("api/exams/{examId:long}/subjects")]
public sealed class ExamSubjectsController(IExamSubjectService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamSubjectDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamSubjectDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromRoute] ulong examId,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(examId, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<IReadOnlyList<ExamSubjectDto>>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromRoute] ulong examId,
        [FromBody] CreateExamSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(examId, request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return Created(
                $"/api/exams/{examId}/subjects",
                ApiResponse<ExamSubjectDto>.Ok(result.Value, "Đã thêm môn thi."));
        }

        return ToErrorResult(result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ExamSubjectDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] ulong examId,
        [FromRoute] ulong id,
        [FromBody] UpdateExamSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(examId, id, request, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamSubjectDto>.Ok(result.Value, "Đã cập nhật môn thi."))
            : ToErrorResult(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] ulong examId,
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(examId, id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<bool>.Ok(true, "Đã xóa môn thi."))
            : ToErrorResult(result);
    }

    private IActionResult ToErrorResult<T>(ServiceResult<T> result)
    {
        var error = result.Error!;
        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "EXAM_NOT_FOUND" or "SUBJECT_NOT_FOUND" or "EXAM_SUBJECT_NOT_FOUND" => NotFound(response),
            "EXAM_SUBJECT_ALREADY_EXISTS" or "EXAM_SUBJECT_HAS_DEPENDENCIES" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
