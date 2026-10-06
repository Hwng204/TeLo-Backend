using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IExamRoomService
{
    Task<ServiceResult<IReadOnlyList<ExamRoomDto>>> ListAsync(
        ulong examId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ExamRoomOptionDto>>> ListOptionsAsync(
        ulong examId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamRoomDto>> GetByIdAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamRoomDto>> CreateAsync(
        ulong examId,
        CreateExamRoomRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExamRoomDto>> UpdateAsync(
        ulong examId,
        ulong id,
        UpdateExamRoomRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken);
}
