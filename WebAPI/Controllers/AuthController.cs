using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Security;

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
        var user = await authenticationService.AuthenticateAsync(request, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });
        }

        return Ok(tokenService.CreateToken(user));
    }
}
