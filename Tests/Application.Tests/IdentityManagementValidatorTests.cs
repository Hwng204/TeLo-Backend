using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class IdentityManagementValidatorTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void RejectsUnboundedPagination(int page, int size) =>
        Assert.NotEmpty(IdentityManagementValidator.Query(new IdentityListQuery { Page = page, PageSize = size }));

    [Fact]
    public void RejectsBranchWithoutSchool() => Assert.Contains("schoolId",
        IdentityManagementValidator.Role(new SaveRoleRequest { Code = "TEST_ROLE", Name = "Role", SchoolBranchId = 2 }).Keys);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsafe code")]
    [InlineData("<script>")]
    public void RejectsInvalidCode(string code) => Assert.Contains("code",
        IdentityManagementValidator.Module(new SaveModuleRequest { Code = code, Name = "Module" }).Keys);

    [Fact]
    public void AcceptsTrimmedVietnameseNameAndScope() => Assert.Empty(IdentityManagementValidator.Role(
        new SaveRoleRequest { Code = "SCHOOL_ADMIN", Name = "  Quản trị trường  ", SchoolId = 1, SchoolBranchId = 2 }));

    [Fact]
    public void RejectsOversizeNameAndDescription()
    {
        var errors = IdentityManagementValidator.Module(new SaveModuleRequest { Code = "MODULE", Name = new string('a',151), Description = new string('a',501) });
        Assert.Contains("name", errors.Keys);
        Assert.Contains("description", errors.Keys);
    }

    [Fact]
    public void RejectsUnknownStatus() => Assert.Contains("status",
        IdentityManagementValidator.Query(new IdentityListQuery { Status = "DELETED" }).Keys);

    [Fact]
    public void LockedStatusIsOnlyValidForAccounts()
    {
        var query = new IdentityListQuery { Status = "LOCKED" };
        Assert.NotEmpty(IdentityManagementValidator.Query(query));
        Assert.Empty(IdentityManagementValidator.Query(query, allowLockedAccount: true));
    }

    [Fact]
    public void AcceptsValidUserCreateRequest()
    {
        var request = new CreateUserRequest
        {
            Username = "  valid.user  ",
            Email = " USER@example.com ",
            FullName = "  Nguyễn Văn A  ",
            Password = "Strong-password-123",
            Status = " active ",
            RoleIds = [1, 2]
        };

        Assert.Empty(IdentityManagementValidator.CreateUser(request));
    }

    [Fact]
    public void RejectsInvalidUserFieldsAndDuplicateRoles()
    {
        var request = new CreateUserRequest
        {
            Username = "a",
            Email = "not-an-email",
            FullName = "\u0001",
            Password = "weak",
            MoetIdentifier = "MOET\u0001",
            SchoolBranchId = 0,
            Status = "UNKNOWN",
            RoleIds = [1, 1]
        };

        var errors = IdentityManagementValidator.CreateUser(request);

        Assert.Contains("username", errors.Keys);
        Assert.Contains("email", errors.Keys);
        Assert.Contains("fullName", errors.Keys);
        Assert.Contains("password", errors.Keys);
        Assert.Contains("moetIdentifier", errors.Keys);
        Assert.Contains("schoolBranchId", errors.Keys);
        Assert.Contains("status", errors.Keys);
        Assert.Contains("roleIds", errors.Keys);
    }

    [Theory]
    [InlineData(" active ")]
    [InlineData("LOCKED")]
    [InlineData("inactive")]
    public void NormalizesValidUserStatus(string status) => Assert.Empty(
        IdentityManagementValidator.UserStatus(new IdentityStatusRequest(status, 1)));

    [Fact]
    public void RejectsWeakResetPassword() => Assert.Contains("newPassword",
        IdentityManagementValidator.ResetPassword(new ResetUserPasswordRequest("short", 1)).Keys);
}
