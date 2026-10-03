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
            new { Code = "PHT", Name = "Phó Hiệu trưởng" },
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
                    IsSystem = true,
                    Name = roleData.Name
                });
            }
        }
        db.SaveChanges();

        var defaultBranchId = db.SchoolBranches
            .Where(branch => branch.Status == "ACTIVE")
            .OrderBy(branch => branch.Id)
            .Select(branch => (ulong?)branch.Id)
            .FirstOrDefault();

        var usersToSeed = new[]
        {
            new { Username = "admin", FullName = "Quản trị viên", RoleCode = "ADMIN" },
            new { Username = "pht", FullName = "Phó Hiệu trưởng", RoleCode = "PHT" },
            new { Username = "teacher", FullName = "Giáo viên", RoleCode = "TEACHER" },
            new { Username = "student", FullName = "Học sinh", RoleCode = "STUDENT" }
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
                    FullName = userData.FullName,
                    SchoolBranchId = userData.RoleCode == "PHT" ? defaultBranchId : null,
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
