using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/matrices/import")]
public sealed class MatrixImportsController(IMatrixImportService service) : ControllerBase
{
    [HttpGet("template.xlsx")]
    public async Task<IActionResult> Template([FromQuery] MatrixImportContext context, CancellationToken cancellationToken)
    {
        var file = await service.BuildTemplateAsync(context, cancellationToken);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<MatrixImportPreview> Preview([FromQuery] MatrixImportContext context, IFormFile? file, CancellationToken cancellationToken)
    {
        var upload = await ReadUploadAsync(file, cancellationToken);
        return await service.PreviewAsync(context, upload.Name, upload.Content, cancellationToken);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<MatrixImportPreview> Import([FromQuery] MatrixImportContext context, IFormFile? file, CancellationToken cancellationToken)
    {
        var upload = await ReadUploadAsync(file, cancellationToken);
        return await service.ImportAsync(context, upload.Name, upload.Content, cancellationToken);
    }

    private static async Task<(string Name, byte[] Content)> ReadUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > MatrixImportWorkbook.MaxFileBytes)
            throw new MatrixApplicationException("ImportFileInvalid", "Vui lòng chọn tệp Excel .xlsx không rỗng và không quá 5 MB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MatrixImportWorkbook.MaxFileBytes)
            throw new MatrixApplicationException("ImportFileInvalid", "Tệp vượt quá 5 MB.");
        return (file.FileName, buffer.ToArray());
    }
}
