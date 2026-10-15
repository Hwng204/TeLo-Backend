using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public sealed class RoleAssignmentQueryTests
{
    [Fact]
    public void InactivePrincipalDoesNotExpandActiveDeputyPrincipalScope()
    {
        var user = UserInBranch();
        var grants = new[]
        {
            Grant(user, new Role { Code = "PRINCIPAL", Status = "INACTIVE" }),
            Grant(user, new Role { Code = "PHT" })
        };

        Assert.Equal(["PHT"], grants.AsQueryable().WhereEffective().Select(grant => grant.Role.Code).ToArray());
    }

    [Fact]
    public void GlobalAdministratorDoesNotRequireAnAssignedBranch()
    {
        var grant = Grant(new User(), new Role { Code = "ADMIN" });
        Assert.Single(new[] { grant }.AsQueryable().WhereEffective());
    }

    [Fact]
    public void EffectiveRoleQueryTranslatesToMySqlWithoutClientFiltering()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=model_validation;User=test;Password=test;", new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new ApplicationDbContext(options);

        var query = context.UserRoles.WhereEffective().Where(grant => grant.UserId == 7).Select(grant => grant.Role.Code).ToQueryString();
        Assert.Contains("ACTIVE", query);
        Assert.Contains("`role`", query);
    }

    private static User UserInBranch() => new()
    {
        SchoolBranchId = 10,
        SchoolBranch = new SchoolBranch { Id = 10, SchoolId = 1, School = new School { Id = 1 } }
    };

    private static UserRole Grant(User user, Role role) => new() { User = user, Role = role };
}
