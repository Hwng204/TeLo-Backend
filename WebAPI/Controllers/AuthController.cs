using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Security;
using System.Security.Claims;
using Application.Validators;
using Application.DTOs;
using Application.Services.Interface;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IUserAuthenticationService authenticationService,
    IJwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!LoginValidator.IsValid(request))
        {
            return BadRequest(new { message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu." });
        }

        var user = await authenticationService.AuthenticateAsync(request, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { code = "Unauthorized", message = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        return Ok(tokenService.CreateToken(user));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.ForgotPasswordAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Error!.Message });
        }

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [AllowAnonymous]
    [HttpPost("verify-otp")]
    public IActionResult VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        [FromServices] IAuthService authService)
    {
        var result = authService.VerifyOtp(request);
        if (!result.IsSuccess)
        {
            if (result.Error!.Message.Contains("3 lần sai"))
            {
                // Parse attempts from message if we want, or just return the message
                return BadRequest(new { message = result.Error.Message });
            }
            return BadRequest(new { message = result.Error.Message });
        }

        return Ok(new { message = "Xác nhận OTP thành công." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Error!.Message, errors = result.Error.Details });
        }

        return Ok(new { message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
    }
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(
        [FromServices] Infrastructure.Context.ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!ulong.TryParse(userIdString, out var userId)) return Unauthorized();

        // Email ← teacher.email (null for accounts without a teacher profile).
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Username, u.FullName, Email = u.Teachers.Select(t => t.Email).FirstOrDefault(), u.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return NotFound();

        return Ok(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!ulong.TryParse(userIdString, out var userId)) return Unauthorized();

        var result = await authService.ChangePasswordAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Error!.Message, errors = result.Error.Details });
        }

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }
}
