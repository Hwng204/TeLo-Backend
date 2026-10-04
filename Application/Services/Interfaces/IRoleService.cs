using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IRoleService
{
    Task<ServiceResult<DirectoryPage<RoleItem>>> ListAsync(IdentityListQuery query, CancellationToken ct);
    Task<ServiceResult<RoleItem>> GetAsync(ulong id, CancellationToken ct);
    Task<ServiceResult<RoleItem>> SaveAsync(ulong? id, SaveRoleRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<RoleItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct);
    Task<ServiceResult<UserRolesDetail>> UserRolesAsync(ulong id, CancellationToken ct);
    Task<ServiceResult<UserRolesDetail>> AssignUserRolesAsync(ulong id, AssignUserRolesRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<RoleItem>> AddUsersAsync(ulong id, AssignRoleUsersRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<RoleItem>> RemoveUserAsync(ulong id, ulong userId, uint version, ulong actor, CancellationToken ct);
    Task<ServiceResult<DirectoryPage<IdentityScopeItem>>> ScopesAsync(string kind, IdentityListQuery query, CancellationToken ct);
}
