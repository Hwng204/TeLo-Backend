using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IModuleService
{
    Task<ServiceResult<DirectoryPage<ModuleItem>>> ListAsync(IdentityListQuery query, CancellationToken ct);
    Task<ServiceResult<ModuleItem>> GetAsync(ulong id, CancellationToken ct);
    Task<ServiceResult<ModuleItem>> SaveAsync(ulong? id, SaveModuleRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<ModuleItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct);
}
