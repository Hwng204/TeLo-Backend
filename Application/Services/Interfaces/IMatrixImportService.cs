using Application.DTOs;

namespace Application.Services.Interface;

public interface IMatrixImportService
{
    Task<MatrixExportFile> BuildTemplateAsync(MatrixImportContext context, CancellationToken cancellationToken);
    Task<MatrixImportPreview> PreviewAsync(MatrixImportContext context, string fileName, byte[] content, CancellationToken cancellationToken);
    Task<MatrixImportPreview> ImportAsync(MatrixImportContext context, string fileName, byte[] content, CancellationToken cancellationToken);
}
