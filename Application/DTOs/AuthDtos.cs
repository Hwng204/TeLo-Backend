namespace Application.DTOs;

public sealed record ForgotPasswordRequest(string Email);
public sealed record VerifyOtpRequest(string Email, string Otp);
public sealed record ResetPasswordRequest(string Email, string Otp, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
