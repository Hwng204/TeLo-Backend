using Application.Common;
using Application.Mappings;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.Notification;
using Xunit;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public sealed class RoleManagementTests
{
    [Fact]
    public void RoleDeletionProjectionTranslatesToMySqlIncludingNotificationReferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=model_validation;User=test;Password=test;", new MySqlServerVersion(new Version(8, 0, 0))).Options;
        using var db = new ApplicationDbContext(options);
        var sql = db.Roles.Select(IdentityProjections.Role(db.NotificationTargets)).ToQueryString();
        Assert.Contains("notification_targets", sql);
        Assert.Contains("used_at", sql);
        Assert.Contains("OPERATIONALADMIN", sql);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("OperationalAdmin")]
    public void AdministratorCannotBeDeletedEvenIfLegacySystemFlagIsMissing(string code)
    {
        var item = IdentityProjections.Role(Array.Empty<NotificationTarget>().AsQueryable()).Compile()(new Role { Code = code });
        Assert.False(item.CanDelete);
        Assert.NotNull(item.DeleteBlockedReason);
    }

    [Fact]
    public void NotificationTargetBlocksDeletionUntilUnlinked()
    {
        var role = new Role { Id = 7, Code = "SCHOOL_ROLE" };
        var linked = IdentityProjections.Role(new[] { new NotificationTarget { RoleId = 7 } }.AsQueryable()).Compile()(role);
        Assert.False(linked.CanDelete);
        Assert.Contains("thông báo", linked.DeleteBlockedReason!);
        var unlinked = IdentityProjections.Role(Array.Empty<NotificationTarget>().AsQueryable()).Compile()(role);
        Assert.True(unlinked.CanDelete);
        Assert.Null(unlinked.DeleteBlockedReason);
    }

    [Fact]
    public void RevokedRoleRetainsHistoryAndCannotBeDeleted()
    {
        var item = IdentityProjections.Role(Array.Empty<NotificationTarget>().AsQueryable()).Compile()(new Role { UsedAt = DateTime.UtcNow });
        Assert.False(item.CanDelete);
        Assert.Contains("lịch sử", item.DeleteBlockedReason!);
    }

    [Theory]
    [InlineData(10, 1, true)]
    [InlineData(11, 1, true)]
    [InlineData(20, 2, false)]
    public void SchoolRoleAppliesToAllBranchesInThatSchool(int branchId, int schoolId, bool expected)
    {
        var role = new Role { SchoolId = 1 };
        var user = new User { SchoolBranchId = (ulong)branchId, SchoolBranch = new SchoolBranch
        {
            Id = (ulong)branchId, SchoolId = (ulong)schoolId, School = new School { Id = (ulong)schoolId }
        }};
        Assert.Equal(expected, IdentityManagementRules.CanAssign(role, user));
    }

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
