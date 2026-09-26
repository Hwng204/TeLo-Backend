using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface ISchoolBranchService
{
    Task<ServiceResult<SchoolBranchDetailDto>> CreateAsync(ulong schoolId, CreateSchoolBranchRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<SchoolBranchDetailDto>> UpdateAsync(ulong id, UpdateSchoolBranchRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteAsync(ulong id, CancellationToken cancellationToken);
}
