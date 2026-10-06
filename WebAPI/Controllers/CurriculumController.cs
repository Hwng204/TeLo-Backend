using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

// Chapters and lessons of the signed-in user's own branch. The branch is never taken from the request.
[ApiController]
[Route("api/curriculum")]
[Authorize(Policy = ReadPolicy)]
public sealed class CurriculumController(
    ICurriculumService service,
    IAuthorizationService authorization) : ControllerBase
{
    public const string ReadPolicy = "CurriculumRead";
    public const string ManagePolicy = "CurriculumManage";

    // A little above the service limit so the service, not the framework, reports an oversize file.
    private const int UploadRequestLimit = CurriculumService.MaxFileBytes + 512 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await service.GetAsync(await ActorAsync(), cancellationToken));

    [HttpPost("chapters")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> CreateChapter(
        [FromBody] SaveChapterRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.CreateChapterAsync(await ActorAsync(), request, cancellationToken),
            "Thêm chương học thành công.");

    [HttpPut("chapters/{chapterId:min(1)}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> UpdateChapter(
        ulong chapterId,
        [FromBody] SaveChapterRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.UpdateChapterAsync(await ActorAsync(), chapterId, request, cancellationToken),
            "Cập nhật chương học thành công.");

    [HttpDelete("chapters/{chapterId:min(1)}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DeleteChapter(ulong chapterId, CancellationToken cancellationToken) =>
        ToActionResult(
            await service.DeleteChapterAsync(await ActorAsync(), chapterId, cancellationToken),
            "Xoá chương học thành công.");

    [HttpPost("chapters/{chapterId:min(1)}/lessons")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> CreateLesson(
        ulong chapterId,
        [FromBody] SaveLessonRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.CreateLessonAsync(await ActorAsync(), chapterId, request, cancellationToken),
            "Thêm bài học thành công.");

    [HttpPut("lessons/{lessonId:min(1)}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> UpdateLesson(
        ulong lessonId,
        [FromBody] SaveLessonRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await service.UpdateLessonAsync(await ActorAsync(), lessonId, request, cancellationToken),
            "Cập nhật bài học thành công.");

    [HttpDelete("lessons/{lessonId:min(1)}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DeleteLesson(ulong lessonId, CancellationToken cancellationToken) =>
        ToActionResult(
            await service.DeleteLessonAsync(await ActorAsync(), lessonId, cancellationToken),
            "Xoá bài học thành công.");

    [HttpGet("import/template.xlsx")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var result = await service.BuildTemplateAsync(cancellationToken);
        return result.Error is null
            ? File(result.Value!.Content, XlsxContentType, result.Value.FileName)
            : ToActionResult(result);
    }

    [HttpPost("import/preview")]
    [Authorize(Policy = ManagePolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public async Task<IActionResult> PreviewImport(IFormFile? file, CancellationToken cancellationToken)
    {
        if (await ReadUploadAsync(file, cancellationToken) is not { } upload)
        {
            return MissingFile<CurriculumImportPreviewDto>();
        }

        return ToActionResult(await service.PreviewImportAsync(
            await ActorAsync(), upload.Name, upload.Content, cancellationToken));
    }

    [HttpPost("import")]
    [Authorize(Policy = ManagePolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (await ReadUploadAsync(file, cancellationToken) is not { } upload)
        {
            return MissingFile<CurriculumImportResultDto>();
        }

        var result = await service.ImportAsync(await ActorAsync(), upload.Name, upload.Content, cancellationToken);
        return ToActionResult(
            result,
            result.Value is { } value
                ? $"Đã nhập {value.NewChapterCount} chương và {value.LessonCount} bài học."
                : null);
    }

    private async Task<CurriculumActor> ActorAsync()
    {
        var userId = ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        var canManage = (await authorization.AuthorizeAsync(User, ManagePolicy)).Succeeded;
        return new CurriculumActor(userId, canManage);
    }

    private static async Task<(string Name, byte[] Content)?> ReadUploadAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return null;
        }

        // Oversize files are not buffered: the service rejects the empty content with the size message.
        if (file.Length > CurriculumService.MaxFileBytes)
        {
            return (file.FileName, Array.Empty<byte>());
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        return (file.FileName, buffer.ToArray());
    }

    private IActionResult MissingFile<T>() => ToActionResult(ServiceResult<T>.Failure(
        CurriculumErrorCodes.ImportFileInvalid,
        "Vui lòng chọn tệp Excel .xlsx.",
        new Dictionary<string, string[]> { ["file"] = ["Vui lòng chọn tệp Excel .xlsx."] }));

    private IActionResult ToActionResult<T>(ServiceResult<T> result, string? successMessage = null)
    {
        if (result.Error is not { } error)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!, successMessage));
        }

        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            CurriculumErrorCodes.Validation or
            CurriculumErrorCodes.ImportFileInvalid or
            CurriculumErrorCodes.ImportHasInvalidRows => UnprocessableEntity(response),
            CurriculumErrorCodes.BranchRequired => StatusCode(StatusCodes.Status403Forbidden, response),
            CurriculumErrorCodes.ChapterNotFound or
            CurriculumErrorCodes.LessonNotFound => NotFound(response),
            CurriculumErrorCodes.ChapterDuplicate or
            CurriculumErrorCodes.LessonDuplicate or
            CurriculumErrorCodes.ChapterInUse or
            CurriculumErrorCodes.LessonInUse or
            CurriculumErrorCodes.ChapterScopeLocked or
            CurriculumErrorCodes.ImportConflict => Conflict(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
