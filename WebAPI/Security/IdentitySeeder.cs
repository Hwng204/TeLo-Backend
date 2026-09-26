using Infrastructure.Context;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace WebAPI.Security;

public static class IdentitySeeder
{
    public static void SeedIdentityData(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        db.Database.EnsureCreated();

        var rolesToSeed = new[]
        {
            new { Code = "ADMIN", Name = "Quản trị viên" },
            new { Code = "TEACHER", Name = "Giáo viên" },
            new { Code = "STUDENT", Name = "Học sinh" }
        };

        foreach (var roleData in rolesToSeed)
        {
            if (!db.Roles.Any(r => r.Code == roleData.Code))
            {
                db.Roles.Add(new Role
                {
                    Code = roleData.Code,
                    Name = roleData.Name
                });
            }
        }
        db.SaveChanges();

        var usersToSeed = new[]
        {
            new { Username = "admin", RoleCode = "ADMIN" },
            new { Username = "teacher", RoleCode = "TEACHER" },
            new { Username = "student", RoleCode = "STUDENT" }
        };

        foreach (var userData in usersToSeed)
        {
            if (!db.Users.Any(u => u.Username == userData.Username))
            {
                var role = db.Roles.First(r => r.Code == userData.RoleCode);
                var newUser = new User
                {
                    Username = userData.Username,
                    Email = $"{userData.Username}@telo.edu.vn",
                    PasswordHash = "",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UserRoles = new List<UserRole>()
                };

                newUser.PasswordHash = passwordHasher.HashPassword(newUser, $"{userData.Username}123");
                newUser.UserRoles.Add(new UserRole
                {
                    RoleId = role.Id
                });
                db.Users.Add(newUser);
            }
        }
        db.SaveChanges();
    }
}
