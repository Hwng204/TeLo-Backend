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

    // role.code is varchar(50) (module codes stay 100).
    [Fact]
    public void RejectsRoleCodeLongerThanColumn() => Assert.Contains("code",
        IdentityManagementValidator.Role(new SaveRoleRequest { Code = new string('A', 51), Name = "Role" }).Keys);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unsafe code")]
    [InlineData("<script>")]
    public void RejectsInvalidCode(string code) => Assert.Contains("code",
        IdentityManagementValidator.Role(new SaveRoleRequest { Code = code, Name = "Vai trò" }).Keys);

    [Fact]
    public void ModuleIgnoresCodeBecauseErdV2HasNoModuleCode() => Assert.Empty(
        IdentityManagementValidator.Module(new SaveModuleRequest { Code = "<script>", Name = "Module" }));

    [Fact]
    public void AcceptsTrimmedVietnameseName() => Assert.Empty(IdentityManagementValidator.Role(
        new SaveRoleRequest { Code = "SCHOOL_ADMIN", Name = "  Quản trị trường  " }));

    [Fact]
    public void NewRoleCanApplyToAllSchools() => Assert.Empty(
        IdentityManagementValidator.Role(new SaveRoleRequest { Code = "TEST_ROLE", Name = "Vai trò" }));


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
            FullName = "\u0001",
            Password = "weak",
            MoetIdentifier = "MOET\u0001",
            SchoolBranchId = 0,
            Status = "UNKNOWN",
            RoleIds = [1, 1]
        };

        var errors = IdentityManagementValidator.CreateUser(request);

        Assert.Contains("username", errors.Keys);
        Assert.DoesNotContain("email", errors.Keys); // no user.email: request email is ignored
        Assert.Contains("fullName", errors.Keys);
        Assert.Contains("password", errors.Keys);
        Assert.Contains("moetIdentifier", errors.Keys);
        Assert.Contains("schoolBranchId", errors.Keys);
        Assert.Contains("status", errors.Keys);
        Assert.Contains("roleIds", errors.Keys);
    }

    [Fact]
    public void RejectsMoetIdentifierLongerThanColumn()
    {
        var request = ValidUpdateUser();
        request.MoetIdentifier = new string('M', 51);

        Assert.Contains("moetIdentifier", IdentityManagementValidator.UpdateUser(request).Keys);
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

    [Fact]
    public void UpdateAllowsPasswordToRemainUnchanged()
    {
        var request = ValidUpdateUser();

        Assert.Empty(IdentityManagementValidator.UpdateUser(request));
    }

    [Fact]
    public void UpdateRejectsWeakNewPassword()
    {
        var request = ValidUpdateUser();
        request.NewPassword = "short";

        Assert.Contains("newPassword", IdentityManagementValidator.UpdateUser(request).Keys);
    }

    [Fact]
    public void UpdateAcceptsStrongNewPassword()
    {
        var request = ValidUpdateUser();
        request.NewPassword = "Strong-password-123";

        Assert.Empty(IdentityManagementValidator.UpdateUser(request));
    }

    private static UpdateUserRequest ValidUpdateUser() => new()
    {
        Username = "valid.user",
        FullName = "Nguyen Van A",
        Version = 1
    };
}
