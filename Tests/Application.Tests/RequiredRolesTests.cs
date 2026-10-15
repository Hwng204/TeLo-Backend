using Application.Common;
using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.Tests;

public sealed class RequiredRolesTests
{
    [Fact]
    public void AllSixRequiredRolesAreReportedMissingWithoutInventingDatabaseRows()
    {
        var status = RequiredRolesService.MissingFrom([]);
        Assert.Equal(6, status.Length);
        Assert.All(status, item => Assert.False(item.Exists));
    }

    [Fact]
    public void ExistingAliasesSatisfyRequiredRolesWithoutDuplicateDefinitions()
    {
        var status = RequiredRolesService.MissingFrom(["Admin", "GIAO_VIEN", "TO_TRUONG", "PRINCIPAL", "HOC_SINH", "PHT"]);
        Assert.All(status, item => Assert.True(item.Exists));
    }
}
