using System.Collections.Generic;
using Application.DTOs;
using Application.Common;

namespace Application.Validators;

public static class AuthValidator
{
    public static Dictionary<string, string[]> ValidateResetPassword(ResetPasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email không được để trống."];
        }

        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            errors["otp"] = ["Mã OTP không được để trống."];
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            errors["newPassword"] = ["Mật khẩu mới phải có ít nhất 6 ký tự."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateChangePassword(ChangePasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            errors["currentPassword"] = ["Mật khẩu hiện tại không được để trống."];
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            errors["newPassword"] = ["Mật khẩu mới phải có ít nhất 6 ký tự."];
        }

        return errors;
    }
}
