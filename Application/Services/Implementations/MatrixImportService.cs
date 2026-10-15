using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Exports;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed class MatrixImportService(IMatrixReferenceRepository references, IMatrixCurrentUser currentUser) : IMatrixImportService
{
    public async Task<MatrixExportFile> BuildTemplateAsync(MatrixImportContext context, CancellationToken cancellationToken)
    {
        var data = await ReferenceAsync(context, cancellationToken);
        var option = data.AcademicContexts.Single(c => c.Id == context.AcademicContextId);
        try
        {
            return new MatrixExportFile($"mau-ma-tran-{context.AcademicContextId}.xlsx",
                MatrixImportWorkbook.CreateTemplate(context.Name, context.TotalScore, option.Label, null, data.Lessons));
        }
        catch (MatrixImportFormatException error) { throw InvalidFile(error.Message); }
    }

    public async Task<MatrixImportPreview> PreviewAsync(MatrixImportContext context, string fileName, byte[] content,
        CancellationToken cancellationToken)
    {
        var data = await ReferenceAsync(context, cancellationToken);
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || content.Length == 0 || content.Length > MatrixImportWorkbook.MaxFileBytes)
            throw InvalidFile($"Vui lòng chọn tệp .xlsx không quá 5 MB và {MatrixImportWorkbook.MaxRows} dòng.");
        try
        {
            return MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(content), data.Lessons, context.TotalScore);
        }
        catch (MatrixImportFormatException error) { throw InvalidFile(error.Message); }
    }

    public async Task<MatrixImportPreview> ImportAsync(MatrixImportContext context, string fileName, byte[] content,
        CancellationToken cancellationToken)
    {
        // Re-read the original file and current references; preview is not permission to bypass validation.
        var preview = await PreviewAsync(context, fileName, content, cancellationToken);
        if (!preview.CanImport)
            throw new MatrixApplicationException("ImportHasInvalidRows",
                $"Tệp có {preview.Errors.Count} lỗi. Vui lòng chọn lại tệp để kiểm tra; bảng ma trận chưa thay đổi.");
        return preview;
    }

    private async Task<MatrixReferenceModel> ReferenceAsync(MatrixImportContext context, CancellationToken cancellationToken)
    {
        if (context.AcademicContextId == 0 || context.TotalScore is <= 0 or > MatrixApplicationService.MaxTotalScore ||
            context.Name?.Length > 255)
            throw new MatrixApplicationException("InvalidRequest",
                $"Chọn phạm vi ma trận, tổng điểm nguyên dương tối đa {MatrixApplicationService.MaxTotalScore} và tên tối đa 255 ký tự.");
        var data = await references.GetReferenceDataAsync(currentUser.Actor, context.AcademicContextId, cancellationToken);
        if (!data.AcademicContexts.Any(c => c.Id == context.AcademicContextId))
            throw new MatrixApplicationException("InvalidReference", "Phạm vi ma trận không hợp lệ hoặc không còn hoạt động.");
        return data;
    }

    private static MatrixApplicationException InvalidFile(string message) => new("ImportFileInvalid", message);
}
