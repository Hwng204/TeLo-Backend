using Domain.Entities.Academic;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Tests.Persistence;

public sealed class OrganizationAcademicModelTests
{
    [Fact]
    public void Model_MapsOrganizationAndAcademicTables()
    {
        var expected = new[]
        {
            "academic_contexts", "academic_years", "classes", "grade_levels",
            "rooms", "school_branches", "schools", "semesters", "subjects",
            "textbook_chapters", "textbook_lessons", "textbooks"
        };

        var actual = ModelFactory.CreateModel().GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && expected.Contains(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.Equal(expected.OrderBy(table => table), actual);
    }

    [Fact]
    public void AcademicContext_UsesCompositeScopeIndexAndBranchSchoolForeignKey()
    {
        var entity = ModelFactory.CreateModel().FindEntityType(typeof(AcademicContext));

        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "uq_academic_contexts_scope" &&
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[]
            {
                "academic_year_id", "school_id", "school_branch_id",
                "textbook_id", "subject_id", "grade_level_id"
            }));

        Assert.Contains(entity.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[]
            {
                "school_branch_id", "school_id"
            }) &&
            foreignKey.PrincipalEntityType.GetTableName() == "school_branches" &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void DateAndUnsignedColumns_UseApprovedRelationalTypes()
    {
        var model = ModelFactory.CreateModel();
        var academicYear = model.FindEntityType(typeof(AcademicYear));
        var chapter = model.FindEntityType(typeof(TextbookChapter));

        Assert.Equal("date", academicYear!.FindProperty(nameof(AcademicYear.StartDate))!.GetColumnType());
        Assert.Equal("date", academicYear.FindProperty(nameof(AcademicYear.EndDate))!.GetColumnType());
        Assert.Equal("bigint unsigned", chapter!.FindProperty(nameof(TextbookChapter.TextbookId))!.GetColumnType());
        Assert.Equal("int unsigned", chapter.FindProperty(nameof(TextbookChapter.SortOrder))!.GetColumnType());
    }
}
