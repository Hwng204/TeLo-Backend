using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs;

namespace Application.Services.Interface;

public interface IAuthService
{
    Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ChangePasswordAsync(
        ulong userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken);

    ServiceResult<bool> VerifyOtp(VerifyOtpRequest request);
}
