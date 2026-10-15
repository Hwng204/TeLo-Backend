using Application.Common;
using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Interface;

public interface IClassService
{
    Task<ServiceResult<DirectoryPage<ClassListItem>>> ListClassesAsync(
        DirectoryScope scope,
        ClassListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ClassDetailDto>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        PageQuery rosterPage,
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

    Task<ServiceResult<SchoolDirectoryReferenceData>> GetReferenceDataAsync(
        DirectoryScope scope,
        ulong? academicYearId,
        CancellationToken cancellationToken);
}
