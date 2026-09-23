using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Organization;

namespace Infrastructure.Tests.Persistence;

public sealed class SchoolDirectoryModelTests
{
    [Fact]
    public void StudentEnrollment_EnforcesOneClassPerAcademicYear()
    {
        var entity = ModelFactory.CreateDesignModel().FindEntityType(typeof(StudentEnrollment));

        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "uq_student_enrollments_student_active_year");
    }

    [Fact]
    public void StudentEnrollment_UsesCompositeClassYearForeignKey()
    {
        var entity = ModelFactory.CreateDesignModel().FindEntityType(typeof(StudentEnrollment));

        Assert.Contains(entity!.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(x => x.GetColumnName())
                .SequenceEqual(new[] { "school_class_id", "academic_year_id" }) &&
            foreignKey.PrincipalEntityType.GetTableName() == "classes");
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "ck_student_enrollments_status");
    }

    [Fact]
    public void Student_HasProfileColumnsAndNoLegacyClassId()
    {
        var entity = ModelFactory.CreateDesignModel().FindEntityType(typeof(Student))!;
        var columns = entity.GetProperties().Select(x => x.GetColumnName()).ToArray();

        foreach (var column in new[] { "code", "full_name", "admission_date", "status" })
        {
            Assert.Contains(column, columns);
        }

        Assert.DoesNotContain("class_id", columns);
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "uq_students_active_code");
        Assert.Contains(entity.GetCheckConstraints(), x =>
            x.Name == "ck_students_status" && x.Sql!.Contains("INACTIVE"));
    }

    [Fact]
    public void SchoolClass_HasCodeUniquePerBranchYear()
    {
        var entity = ModelFactory.CreateDesignModel().FindEntityType(typeof(SchoolClass))!;

        Assert.Contains("code", entity.GetProperties().Select(x => x.GetColumnName()));
        Assert.Contains(entity.GetIndexes(), x =>
            x.IsUnique && x.GetDatabaseName() == "uq_classes_branch_year_code");
    }
}
