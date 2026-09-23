using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

// Shared plumbing for the school and admin import controllers.
public abstract class StudentImportControllerBase : SchoolDirectoryControllerBase
{
    // A little above StudentImportService.MaxFileBytes so the service, not the framework, reports
    // an oversize file with the friendly message; the request cap still stops abuse.
    public const int UploadRequestLimit = StudentImportService.MaxFileBytes + 512 * 1024;

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    protected IActionResult ToFileResult(ServiceResult<StudentImportFile> result) =>
        result.Error is null
            ? File(result.Value!.Content, XlsxContentType, result.Value.FileName)
            : ToActionResult(result);

    protected async Task<IActionResult> UploadAsync(
        IStudentImportService service,
        ImportCaller caller,
        IFormFile? file,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return ToActionResult(ServiceResult<StudentImportBatchDetailDto>.Failure(
                SchoolDirectoryErrorCodes.Validation,
                "Thiếu file Excel.",
                new Dictionary<string, string[]> { ["file"] = ["Vui lòng chọn file Excel .xlsx."] }));
        }

        // Check before buffering so an oversize upload is never copied into memory.
        if (file.Length > StudentImportService.MaxFileBytes)
        {
            return ToActionResult(ServiceResult<StudentImportBatchDetailDto>.Failure(
                SchoolDirectoryErrorCodes.ImportFileInvalid,
                $"File vượt quá {StudentImportService.MaxFileBytes / (1024 * 1024)} MB."));
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        return ToActionResult(await service.PreviewAsync(
            caller, academicYearId, file.FileName, buffer.ToArray(), cancellationToken));
    }
}
