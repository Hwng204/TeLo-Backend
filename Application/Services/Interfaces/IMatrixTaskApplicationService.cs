using Application.DTOs;


namespace Application.Services.Interface;

public interface IMatrixTaskApplicationService
{
    Task<MatrixTaskResponse> CreateAsync(
        CreateMatrixTaskRequest request,
        CancellationToken cancellationToken);

    Task<MatrixTaskResponse> GetAsync(
        ulong taskId,
        CancellationToken cancellationToken);

    Task<MatrixReferenceData> GetReferenceDataAsync(
        ulong? academicContextId,
        CancellationToken cancellationToken);
}
