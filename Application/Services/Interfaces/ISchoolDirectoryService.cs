using Application.Common;
using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Interface;

// One read API for both callers. School routes pass DirectoryScope.FromActor, admin routes pass
// DirectoryScope.ForSchool with the schoolId taken from the route.
public interface ISchoolDirectoryService
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

    Task<ServiceResult<DirectoryPage<ClassListItem>>> ListClassesAsync(
        DirectoryScope scope,
        ClassListQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<ClassDetailDto>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        PageQuery rosterPage,
        CancellationToken cancellationToken);

    Task<ServiceResult<SchoolDirectoryReferenceData>> GetReferenceDataAsync(
        DirectoryScope scope,
        ulong? academicYearId,
        CancellationToken cancellationToken);
}
