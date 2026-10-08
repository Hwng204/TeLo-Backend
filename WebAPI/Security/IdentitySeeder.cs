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


    }
}
