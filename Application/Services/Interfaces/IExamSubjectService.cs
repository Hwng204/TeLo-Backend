using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IExamSubjectService
{
    Task<ServiceResult<IReadOnlyList<ExamSubjectDto>>> ListAsync(
        ulong examId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamSubjectDto>> CreateAsync(
        ulong examId,
        CreateExamSubjectRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamSubjectDto>> UpdateAsync(
        ulong examId,
        ulong id,
        UpdateExamSubjectRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);
}
