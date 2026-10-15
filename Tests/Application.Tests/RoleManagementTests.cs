using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.Notification;
using Infrastructure.Security;
using Xunit;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public sealed class RoleManagementTests
{
    private static readonly string[] SystemCodes = IdentityManagementRules.SystemRoleCodes(new Catalog(["PHT"], ["TO_TRUONG"], ["HIEU_TRUONG"]));

    private sealed record Catalog(string[] PhtRoleCodes, string[] TeamLeadRoleCodes, string[] PrincipalRoleCodes) : IMatrixRoleCatalog
    {
        IReadOnlyCollection<string> IMatrixRoleCatalog.PhtRoleCodes => PhtRoleCodes;
        IReadOnlyCollection<string> IMatrixRoleCatalog.TeamLeadRoleCodes => TeamLeadRoleCodes;
        IReadOnlyCollection<string> IMatrixRoleCatalog.PrincipalRoleCodes => PrincipalRoleCodes;
        public bool IsPht(string roleCode) => PhtRoleCodes.Contains(roleCode);
        public bool IsTeamLead(string roleCode) => TeamLeadRoleCodes.Contains(roleCode);
        public bool IsPrincipal(string roleCode) => PrincipalRoleCodes.Contains(roleCode);
    }

    private static RoleItem Project(Role role) => IdentityProjections.Role(SystemCodes).Compile()(role);

    [Fact]
    public void RoleDeletionProjectionTranslatesToMySql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=model_validation;User=test;Password=test;", new MySqlServerVersion(new Version(8, 0, 0))).Options;
        using var db = new ApplicationDbContext(options);
        var sql = db.Roles.Select(IdentityProjections.Role(SystemCodes)).ToQueryString();
        Assert.Contains("permission", sql);
        Assert.Contains("OPERATIONALADMIN", sql);
        Assert.DoesNotContain("used_at", sql);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("OperationalAdmin")]
    [InlineData("TEACHER")]
    [InlineData("pht")]
    public void SystemRoleIsDerivedFromCodeAndCannotBeDeleted(string code)
    {
        var item = Project(new Role { Code = code });
        Assert.True(item.IsSystem);
        Assert.False(item.CanDelete);
        Assert.NotNull(item.DeleteBlockedReason);
    }

    [Fact]
    public void CatalogConfiguredRoleCodeIsSystem()
    {
        Assert.Contains("CUSTOM_LEAD", IdentityManagementRules.SystemRoleCodes(new Catalog([], [" custom_lead "], [])));
    }

    [Fact]
    public void UnusedCustomRoleCanBeDeleted()
    {
        var role = new Role { Id = 7, Code = "SCHOOL_ROLE" };
        var unlinked = Project(role);
        Assert.False(unlinked.IsSystem);
        Assert.True(unlinked.CanDelete);
        Assert.Null(unlinked.DeleteBlockedReason);
    }

    [Fact]
    public void AssignedRoleCannotBeDeletedButIsDeletableAfterRevocation()
    {
        var role = new Role { Code = "CUSTOM_ROLE", UserRoles = { new UserRole() } };
        var assigned = Project(role);
        Assert.False(assigned.CanDelete);
        Assert.Contains("gán", assigned.DeleteBlockedReason!);
        role.UserRoles.Clear();
        Assert.True(Project(role).CanDelete); // no used_at history rule any more
    }

    [Theory]
    [InlineData("ACTIVE", "ACTIVE", true)]
    [InlineData("INACTIVE", "ACTIVE", false)]
    [InlineData("ACTIVE", "LOCKED", false)]
    public void CanAssignOnlyChecksRoleAndAccountStatus(string roleStatus, string userStatus, bool expected) =>
        Assert.Equal(expected, IdentityManagementRules.CanAssign(new Role { Status = roleStatus }, new User { Status = userStatus }));

    [Theory]
    [InlineData(1, 10)]
    [InlineData(1, 11)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    public void AllSchoolsRoleAppliesToBranchesOfEverySchool(int schoolId, int branchId)
    {
        var user = new User { SchoolBranchId = (ulong)branchId, SchoolBranch = new SchoolBranch
        {
            Id = (ulong)branchId, SchoolId = (ulong)schoolId, School = new School { Id = (ulong)schoolId }
        }};
        Assert.True(IdentityManagementRules.CanAssign(new Role(), user));
    }
}
