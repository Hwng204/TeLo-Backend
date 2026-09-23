using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

// Write side of the directory. Only admin routes reach it and the school always comes from the route.
public interface ISchoolDirectoryAdminService
{
    Task<ServiceResult<StudentDetailDto>> CreateStudentAsync(
        ulong schoolId,
        CreateStudentRequest request,
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

    Task<ServiceResult<ClassDetailDto>> CreateClassAsync(
        ulong schoolId,
        CreateClassRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ClassDetailDto>> UpdateClassAsync(
        ulong schoolId,
        ulong classId,
        UpdateClassRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ClassDetailDto>> DeleteClassAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken);
}
