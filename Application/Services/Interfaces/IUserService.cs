using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IUserService
{
    Task<ServiceResult<DirectoryPage<IdentityUserItem>>> ListAsync(IdentityListQuery query, CancellationToken ct);
    Task<ServiceResult<UserDetailItem>> GetAsync(ulong id, CancellationToken ct);
    Task<ServiceResult<UserDetailItem>> CreateAsync(CreateUserRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<UserDetailItem>> UpdateAsync(ulong id, UpdateUserRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<UserDetailItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<UserDetailItem>> ResetPasswordAsync(ulong id, ResetUserPasswordRequest request, ulong actor, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct);
}
