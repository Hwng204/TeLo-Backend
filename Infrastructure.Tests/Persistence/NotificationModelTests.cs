using Domain.Entities.Notification;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class NotificationModelTests
{
    [Fact]
    public void Model_MapsNotificationTables()
    {
        var expected = new[]
        {
            "notification_configs", "notification_recipients", "notification_targets", "notifications"
        };

        var actual = ModelFactory.CreateModel().GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && expected.Contains(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.Equal(expected.OrderBy(table => table), actual);
    }

    [Fact]
    public void NotificationFields_UseApprovedTypesAndDefaults()
    {
        var model = ModelFactory.CreateModel();
        var config = model.FindEntityType(typeof(NotificationConfig))!;
        var notification = model.FindEntityType(typeof(Notification))!;
        var recipient = model.FindEntityType(typeof(NotificationRecipient))!;

        Assert.Equal("longtext", config.FindProperty(nameof(NotificationConfig.ContentTemplate))!.GetColumnType());
        Assert.True(config.FindProperty(nameof(NotificationConfig.SchoolId))!.IsNullable);
        Assert.Equal("datetime(6)", notification.FindProperty(nameof(Notification.CreatedAt))!.GetColumnType());
        Assert.Equal("CURRENT_TIMESTAMP(6)", notification.FindProperty(nameof(Notification.CreatedAt))!.GetDefaultValueSql());
        Assert.Equal("PENDING", recipient.FindProperty(nameof(NotificationRecipient.EmailStatus))!.GetDefaultValue());
    }

    [Fact]
    public void NotificationRelationships_PreserveCompositeScopeAndDeleteBehavior()
    {
        var model = ModelFactory.CreateModel();
        var config = model.FindEntityType(typeof(NotificationConfig))!;
        var recipient = model.FindEntityType(typeof(NotificationRecipient))!;

        Assert.Contains(config.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "school_branch_id", "school_id" }) &&
            foreignKey.PrincipalEntityType.GetTableName() == "school_branches" &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(recipient.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "notification_id" }) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(config.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "uq_notification_configs_branch_override");
    }

    [Fact]
    public void GeneratedScopeColumns_AreShadowPersistenceProperties()
    {
        var model = ModelFactory.CreateModel();
        var config = model.FindEntityType(typeof(NotificationConfig))!;
        var generated = config.GetProperties().Where(property => property.GetComputedColumnSql() is not null).ToArray();

        Assert.Equal(2, generated.Length);
        Assert.All(generated, property => Assert.True(property.IsShadowProperty()));
        Assert.Contains(generated, property => property.GetColumnName() == "base_scope_school_key" && property.GetColumnType() == "bigint unsigned");
        Assert.Contains(generated, property => property.GetColumnName() == "base_scope_code_key" && property.GetColumnType() == "varchar(100)");
        Assert.Contains(config.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "uq_notification_configs_base_scope" &&
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "base_scope_school_key", "base_scope_code_key" }));
        Assert.Null(typeof(NotificationConfig).GetProperty("BaseScopeSchoolKey"));
        Assert.Null(typeof(NotificationConfig).GetProperty("BaseScopeCodeKey"));
    }
}
