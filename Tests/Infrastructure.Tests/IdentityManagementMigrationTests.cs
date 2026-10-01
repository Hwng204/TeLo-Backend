using Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class IdentityManagementMigrationTests
{
    [Fact]
    public void UpgradeIsAdditiveAndBackfillsModuleCodesBeforeUniqueIndex()
    {
        var operations = new TestableMigration().Operations();
        Assert.DoesNotContain(operations, operation => operation is DropColumnOperation or DropTableOperation or DropForeignKeyOperation);
        var index = operations.ToList().FindIndex(operation => operation is CreateIndexOperation { Name: "uq_modules_code" });
        var backfill = operations.ToList().FindIndex(operation => operation is SqlOperation sql && sql.Sql.Contains("UPDATE modules SET code"));
        Assert.True(backfill >= 0 && index > backfill);
        Assert.Contains(operations, operation => operation is SqlOperation sql && sql.Sql.Contains("BASELINE_GRANT"));
    }

    private sealed class TestableMigration : AddIdentityManagement
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            Up(builder);
            return builder.Operations;
        }
    }
}
