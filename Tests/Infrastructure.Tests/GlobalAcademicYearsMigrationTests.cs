using Infrastructure.Migrations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class GlobalAcademicYearsMigrationTests
{
    [Fact]
    public void Up_PreservesSchemaAlreadyMigratedByAcademicYearSystemScope()
    {
        // Repeating the province FK drop fails both a fresh migration chain and
        // existing databases where AcademicYearSystemScope has already run.
        // Unrelated legacy school columns must also retain their data.
        var migration = new GlobalAcademicYears();
        Assert.Empty(migration.UpOperations);
    }

    [Fact]
    public void Down_LeavesGlobalSchemaForAcademicYearSystemScopeToRevert()
    {
        // Rolling back this duplicate must not introduce a PascalCase shadow
        // province column or recreate school columns which were never removed.
        var migration = new GlobalAcademicYears();
        Assert.Empty(migration.DownOperations);
    }
}
