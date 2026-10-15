using Application.Common;
using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Interface;

public interface IStudentService
{
    Task<ServiceResult<DirectoryPage<StudentListItem>>> ListStudentsAsync(
        DirectoryScope scope,
        StudentListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentDetailDto>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken);

    Task<ServiceResult<DirectoryPage<StudentScoreItem>>> GetStudentScoresAsync(
        DirectoryScope scope,
        ulong studentId,
        ulong classId,
        PageQuery page,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentDetailDto>> CreateStudentAsync(
        ulong schoolId,
        CreateStudentRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<string>> SuggestStudentCodeAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentDetailDto>> UpdateStudentAsync(
        ulong schoolId,
        ulong studentId,
        UpdateStudentRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentDetailDto>> TransferStudentClassAsync(
        ulong schoolId,
        ulong studentId,
        TransferStudentClassRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<StudentDetailDto>> DeleteStudentAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken);
}
