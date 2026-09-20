using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IExamService
{
    Task<ServiceResult<ExamDetailDto>> CreateAsync(
        CreateExamRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamPage>> ListAsync(
        ExamListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamDetailDto>> GetByIdAsync(
        ulong id,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamDetailDto>> UpdateAsync(
        ulong id,
        UpdateExamRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        ulong id,
        CancellationToken cancellationToken);
}
