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
}
