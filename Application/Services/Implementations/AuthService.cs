using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Application.Validators;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services.Implement;

public sealed class AuthService(
    ApplicationDbContext db,
    IMemoryCache cache,
    IPasswordHasher<User> passwordHasher,
    IEmailService emailService) : IAuthService
{
    public async Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidator.ValidateResetPassword(request);
        if (errors.Count > 0)
        {
            return ServiceResult<bool>.Failure("VALIDATION_ERROR", "Dữ liệu gửi lên không hợp lệ.", errors);
        }

        var email = request.Email.Trim();

        if (!cache.TryGetValue($"OTP_{email}", out string? storedOtp) || storedOtp != request.Otp)
        {
            return ServiceResult<bool>.Failure("INVALID_OTP", "Mã OTP không chính xác hoặc đã hết hạn.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Status == "ACTIVE", cancellationToken);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("USER_NOT_FOUND", "Người dùng không hợp lệ.");
        }

        // Update password
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityVersion++; // Invalidate existing sessions
        await db.SaveChangesAsync(cancellationToken);

        // Remove OTP
        cache.Remove($"OTP_{email}");

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ChangePasswordAsync(
        ulong userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidator.ValidateChangePassword(request);
        if (errors.Count > 0)
        {
            return ServiceResult<bool>.Failure("VALIDATION_ERROR", "Dữ liệu gửi lên không hợp lệ.", errors);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return ServiceResult<bool>.Failure("INVALID_PASSWORD", "Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityVersion++; // Invalidate existing sessions
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return ServiceResult<bool>.Failure("VALIDATION_ERROR", "Email không hợp lệ.");

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email && u.Status == "ACTIVE", cancellationToken);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("USER_NOT_FOUND", "Email giáo viên không tồn tại vui lòng kiểm tra lại.");
        }

        // Generate 6-digit OTP
        var otp = System.Random.Shared.Next(100000, 999999).ToString();
        
        // Store OTP in cache for 5 minutes
        cache.Set($"OTP_{email}", otp, System.TimeSpan.FromMinutes(5));
        cache.Set($"OTP_Attempts_{email}", 0, System.TimeSpan.FromMinutes(5));

        // Simulate sending email
        await emailService.SendEmailAsync(
            email,
            "Yêu cầu lấy lại mật khẩu",
            $"Mã OTP của bạn là: {otp}. Mã có hiệu lực trong 5 phút.",
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public ServiceResult<bool> VerifyOtp(VerifyOtpRequest request)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Otp))
            return ServiceResult<bool>.Failure("VALIDATION_ERROR", "Vui lòng nhập đầy đủ thông tin.");

        if (!cache.TryGetValue($"OTP_{email}", out string? storedOtp))
        {
            return ServiceResult<bool>.Failure("OTP_EXPIRED", "Mã OTP đã hết hạn hoặc không tồn tại.");
        }

        int attempts = cache.TryGetValue($"OTP_Attempts_{email}", out int a) ? a : 0;
        if (attempts >= 3)
        {
            cache.Remove($"OTP_{email}");
            cache.Remove($"OTP_Attempts_{email}");
            return ServiceResult<bool>.Failure("OTP_BLOCKED", "Bạn đã nhập sai OTP quá 3 lần, vui lòng gửi lại yêu cầu để nhận mã mới.");
        }

        if (storedOtp != request.Otp?.Trim())
        {
            attempts++;
            cache.Set($"OTP_Attempts_{email}", attempts, System.TimeSpan.FromMinutes(5));
            if (attempts >= 3)
            {
                cache.Remove($"OTP_{email}");
                cache.Remove($"OTP_Attempts_{email}");
                return ServiceResult<bool>.Failure("OTP_BLOCKED", "Bạn đã nhập sai OTP quá 3 lần, vui lòng gửi lại yêu cầu để nhận mã mới.");
            }
            return ServiceResult<bool>.Failure("INVALID_OTP", $"Mã OTP không chính xác ({attempts}/3 lần sai).");
        }

        return ServiceResult<bool>.Success(true);
    }
}
