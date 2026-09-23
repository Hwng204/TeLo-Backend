using Domain.Entities.Identity;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class IdentityModelTests
{
    [Fact]
    public void Model_MapsIdentityTables()
    {
        var expected = new[]
        {
            "modules", "navbars", "permissions", "roles",
            "students", "teachers", "user_roles", "users"
        };

        var actual = ModelFactory.CreateModel().GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && expected.Contains(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.Equal(expected.OrderBy(table => table), actual);
    }

    [Fact]
    public void Permission_UsesCompositePrimaryKey()
    {
        var entity = ModelFactory.CreateModel().FindEntityType(typeof(Permission));

        Assert.NotNull(entity);
        Assert.Equal(new[] { "role_id", "navbar_id" },
            entity!.FindPrimaryKey()!.Properties.Select(property => property.GetColumnName()));
    }

    [Fact]
    public void IdentityRelationships_PreserveSelfReferenceAndNullableForeignKeys()
    {
        var model = ModelFactory.CreateModel();
        var navbar = model.FindEntityType(typeof(Navbar))!;
        var teacher = model.FindEntityType(typeof(Teacher))!;
        var user = model.FindEntityType(typeof(User))!;

        Assert.Contains(navbar.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "parent_id" }) &&
            foreignKey.PrincipalEntityType.GetTableName() == "navbars" &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.False(teacher.FindProperty(nameof(Teacher.ClassId))!.IsNullable == false);
        Assert.True(user.FindProperty(nameof(User.SchoolBranchId))!.IsNullable);
    }
}
