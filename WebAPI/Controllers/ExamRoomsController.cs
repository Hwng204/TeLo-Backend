using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "ExamManager")]
[Route("api/exams/{examId:long}/rooms")]
public sealed class ExamRoomsController(IExamRoomService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamRoomDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamRoomDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromRoute] ulong examId,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(examId, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<IReadOnlyList<ExamRoomDto>>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpGet("options")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamRoomOptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamRoomOptionDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListOptions(
        [FromRoute] ulong examId,
        CancellationToken cancellationToken)
    {
        var result = await service.ListOptionsAsync(examId, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<IReadOnlyList<ExamRoomOptionDto>>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] ulong examId,
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(examId, id, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamRoomDto>.Ok(result.Value))
            : ToErrorResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromRoute] ulong examId,
        [FromBody] CreateExamRoomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(examId, request, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            return CreatedAtAction(
                nameof(GetById),
                new { examId, id = result.Value.Id },
                ApiResponse<ExamRoomDto>.Ok(result.Value, "Đã thêm phòng thi."));
        }

        return ToErrorResult(result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ExamRoomDto>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        [FromRoute] ulong examId,
        [FromRoute] ulong id,
        [FromBody] UpdateExamRoomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(examId, id, request, cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Ok(ApiResponse<ExamRoomDto>.Ok(result.Value, "Đã cập nhật phòng thi."))
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
            ? Ok(ApiResponse<bool>.Ok(true, "Đã xóa phòng thi."))
            : ToErrorResult(result);
    }

    private IActionResult ToErrorResult<T>(ServiceResult<T> result)
    {
        var error = result.Error!;
        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            "VALIDATION_ERROR" => UnprocessableEntity(response),
            "EXAM_NOT_FOUND" or "ROOM_NOT_FOUND" or "EXAM_ROOM_NOT_FOUND" => NotFound(response),
            "EXAM_ROOM_CODE_ALREADY_EXISTS" or "EXAM_ROOM_ALREADY_EXISTS" or
                "EXAM_ROOM_HAS_DEPENDENCIES" => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
