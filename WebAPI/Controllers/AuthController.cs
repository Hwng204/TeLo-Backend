using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebAPI.Security;
using System.Security.Claims;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IUserAuthenticationService authenticationService,
    IJwtTokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("seed-test")]
    public async Task<IActionResult> SeedTestUser(
        [FromServices] Infrastructure.Context.ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        try {
            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Entities.Identity.User>();
            
            // 1. Create School
            var school = await db.Schools.FirstOrDefaultAsync(s => s.Code == "TEST_SCHOOL", cancellationToken);
            if (school == null)
            {
                school = new Domain.Entities.Organization.School { Code = "TEST_SCHOOL", Name = "Trường Test", Status = "ACTIVE" };
                db.Schools.Add(school);
                await db.SaveChangesAsync(cancellationToken);
            }

            // 2. Create Branch
            var branch = await db.SchoolBranches.FirstOrDefaultAsync(b => b.Code == "TEST_BRANCH", cancellationToken);
            if (branch == null)
            {
                branch = new Domain.Entities.Organization.SchoolBranch { Code = "TEST_BRANCH", Name = "Cơ sở Test", Status = "ACTIVE", SchoolId = school.Id };
                db.SchoolBranches.Add(branch);
                await db.SaveChangesAsync(cancellationToken);
            }

            // 3. Create Roles
            var roleDefinitions = new[]
            {
                new { Code = "STUDENT", Name = "Student" },
                new { Code = "TEACHER", Name = "Teacher" },
                new { Code = "PRINCIPAL", Name = "Principal" },
                new { Code = "VICE_PRINCIPAL", Name = "Vice Principal" },
                new { Code = "ADMIN", Name = "Admin" },
                new { Code = "SUBJECT_COORDINATOR", Name = "Subject Coordinator" }
            };

            Domain.Entities.Identity.Role? teacherRole = null;
            foreach (var def in roleDefinitions)
            {
                var r = await db.Roles.FirstOrDefaultAsync(x => x.Code == def.Code, cancellationToken);
                if (r == null)
                {
                    r = new Domain.Entities.Identity.Role { Code = def.Code, Name = def.Name };
                    db.Roles.Add(r);
                }
                if (def.Code == "TEACHER") teacherRole = r;
            }
            await db.SaveChangesAsync(cancellationToken);

            // 4. Create User
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "hungdz99204@gmail.com", cancellationToken);
            if (user == null)
            {
                user = new Domain.Entities.Identity.User
                {
                    Username = "GV0001",
                    Email = "hungdz99204@gmail.com",
                    FullName = "Hưng Teacher",
                    SchoolBranchId = branch.Id,
                    Status = "ACTIVE",
                    CreatedAt = System.DateTime.UtcNow
                };
                user.PasswordHash = passwordHasher.HashPassword(user, "123456");
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);

                db.UserRoles.Add(new Domain.Entities.Identity.UserRole { UserId = user.Id, RoleId = teacherRole!.Id });
                db.Teachers.Add(new Domain.Entities.Identity.Teacher { UserId = user.Id, StaffCode = "GV0001", EmploymentStatus = "WORKING" });
                await db.SaveChangesAsync(cancellationToken);
            }
            else 
            {
                user.Username = "GV0001";
                user.PasswordHash = passwordHasher.HashPassword(user, "123456");
                user.Status = "ACTIVE";
                
                var existingTeacher = await db.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id, cancellationToken);
                if (existingTeacher == null)
                {
                    db.Teachers.Add(new Domain.Entities.Identity.Teacher { UserId = user.Id, StaffCode = "GV0001", EmploymentStatus = "WORKING" });
                }
                else
                {
                    existingTeacher.StaffCode = "GV0001";
                }
                
                if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == teacherRole!.Id, cancellationToken))
                {
                    db.UserRoles.Add(new Domain.Entities.Identity.UserRole { UserId = user.Id, RoleId = teacherRole!.Id });
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { 
                message = "Đã khởi tạo dữ liệu mẫu thành công!", 
                account = new { 
                    email = "hungdz99204@gmail.com",
                    username = "hungdz",
                    password = "123456" 
                } 
            });
        } catch (System.Exception ex) {
            return BadRequest(new { error = ex.ToString() });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
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
        [FromServices] Infrastructure.Context.ApplicationDbContext db,
        [FromServices] Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        [FromServices] Application.Services.Interface.IEmailService emailService,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email không hợp lệ." });

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email && u.Status == "ACTIVE", cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "Email giáo viên không tồn tại vui lòng kiểm tra lại." });
        }

        // Generate 6-digit OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();
        
        // Store OTP in cache for 5 minutes
        cache.Set($"OTP_{email}", otp, TimeSpan.FromMinutes(5));
        cache.Set($"OTP_Attempts_{email}", 0, TimeSpan.FromMinutes(5));

        // Simulate sending email
        await emailService.SendEmailAsync(
            email,
            "Yêu cầu lấy lại mật khẩu",
            $"Mã OTP của bạn là: {otp}. Mã có hiệu lực trong 5 phút.",
            cancellationToken);

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    [AllowAnonymous]
    [HttpPost("verify-otp")]
    public IActionResult VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        [FromServices] Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Otp))
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });

        if (!cache.TryGetValue($"OTP_{email}", out string? storedOtp))
        {
            return BadRequest(new { message = "Mã OTP đã hết hạn hoặc không tồn tại." });
        }

        int attempts = cache.TryGetValue($"OTP_Attempts_{email}", out int a) ? a : 0;
        if (attempts >= 3)
        {
            cache.Remove($"OTP_{email}");
            cache.Remove($"OTP_Attempts_{email}");
            return BadRequest(new { message = "Bạn đã nhập sai OTP quá 3 lần, vui lòng gửi lại yêu cầu để nhận mã mới." });
        }

        if (storedOtp != request.Otp?.Trim())
        {
            attempts++;
            cache.Set($"OTP_Attempts_{email}", attempts, TimeSpan.FromMinutes(5));
            if (attempts >= 3)
            {
                cache.Remove($"OTP_{email}");
                cache.Remove($"OTP_Attempts_{email}");
                return BadRequest(new { message = "Bạn đã nhập sai OTP quá 3 lần, vui lòng gửi lại yêu cầu để nhận mã mới.", attempts });
            }
            return BadRequest(new { message = $"Mã OTP không chính xác ({attempts}/3 lần sai).", attempts });
        }

        return Ok(new { message = "Xác nhận OTP thành công." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] Infrastructure.Context.ApplicationDbContext db,
        [FromServices] Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        [FromServices] Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.Identity.User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Otp) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });

        if (!cache.TryGetValue($"OTP_{email}", out string? storedOtp) || storedOtp != request.Otp)
        {
            return BadRequest(new { message = "Mã OTP không chính xác hoặc đã hết hạn." });
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Status == "ACTIVE", cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "Người dùng không hợp lệ." });
        }

        // Update password
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityVersion++; // Invalidate existing sessions
        await db.SaveChangesAsync(cancellationToken);

        // Remove OTP
        cache.Remove($"OTP_{email}");

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

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return NotFound();

        return Ok(new
        {
            user.Username,
            user.FullName,
            user.Email,
            user.Status
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] Infrastructure.Context.ApplicationDbContext db,
        [FromServices] Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.Identity.User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!ulong.TryParse(userIdString, out var userId)) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return NotFound();

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verificationResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không chính xác." });
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityVersion++; // Invalidate existing sessions
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }
}

public sealed record ForgotPasswordRequest(string Email);
public sealed record VerifyOtpRequest(string Email, string Otp);
public sealed record ResetPasswordRequest(string Email, string Otp, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
