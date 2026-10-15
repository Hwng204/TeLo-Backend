using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class RevisionTokenTests
{
    [Fact]
    public void TokensAreStableAndChangeWithMutableFieldsAndRelationships()
    {
        var role = new RoleItem(7, "TEACHER", "Giáo viên", "ACTIVE", true, 2, false);
        Assert.Equal(role.Version, (role with { }).Version);
        Assert.NotEqual(role.Version, (role with { Name = "Tên mới" }).Version);
        Assert.NotEqual(role.Version, (role with { UserCount = 3 }).Version);
        Assert.NotEqual(role.Version, (role with { Status = "INACTIVE" }).Version);
        Assert.NotEqual(0u, RevisionToken.For(""));
    }
}
