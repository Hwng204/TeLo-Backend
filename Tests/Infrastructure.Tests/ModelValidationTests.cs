using Domain.Entities.Examination;
using Domain.Entities.Academic;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public sealed class ModelValidationTests
{
    [Fact]
    public void Model_EnforcesAtMostOneActiveAcademicYearPerProvince()
    {
        using var context = CreateContext();
        var academicYear = context.Model.FindEntityType(typeof(AcademicYear));

        Assert.NotNull(academicYear);
        var activeSystemKey = academicYear.FindProperty(nameof(AcademicYear.ActiveSystemKey));
        Assert.NotNull(activeSystemKey);
        Assert.NotNull(activeSystemKey.GetComputedColumnSql());
        var uniqueIndex = Assert.Single(
            academicYear.GetIndexes(),
            index => index.Properties.SequenceEqual([activeSystemKey]));
        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void Model_UsesTheIdentityStudentForExamRegistration()
    {
        using var context = CreateContext();
        var registration = context.Model.FindEntityType(typeof(ExamRegistration));

        Assert.NotNull(registration);
        var studentForeignKey = Assert.Single(
            registration.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == "StudentId"));
        Assert.Equal(
            typeof(Domain.Entities.Identity.Student),
            studentForeignKey.PrincipalEntityType.ClrType);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Database=model_validation;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ApplicationDbContext(options);
    }
}
