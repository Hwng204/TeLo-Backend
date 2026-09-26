using Domain.Entities.Academic;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearDomainTests
{
    [Fact]
    public void Activate_RejectsAClosedAcademicYear()
    {
        var year = CreateYear("CLOSED");

        var exception = Assert.Throws<AcademicCalendarDomainException>(year.Activate);

        Assert.Equal("ACADEMIC_YEAR_CLOSED", exception.Code);
    }

    [Fact]
    public void Close_ClosesTheAcademicYearAndEveryOpenSemester()
    {
        var year = CreateYear("ACTIVE");
        year.Semesters.Add(new Semester { Id = 10, Order = 1, Status = "ACTIVE" });
        year.Semesters.Add(new Semester { Id = 11, Order = 2, Status = "PLANNED" });

        year.Close();

        Assert.Equal("CLOSED", year.Status);
        Assert.All(year.Semesters, semester => Assert.Equal("CLOSED", semester.Status));
    }

    [Fact]
    public void EnsureCanUpdateSchedule_RejectsDatesThatExcludeAnExistingSemester()
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester
        {
            Order = 1,
            Name = "Học kỳ 1",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 1, 15)
        });

        var exception = Assert.Throws<AcademicCalendarDomainException>(() =>
            year.EnsureCanUpdateSchedule(
                new DateOnly(2026, 9, 2),
                new DateOnly(2027, 5, 31)));

        Assert.Equal("SEMESTER_OUT_OF_BOUNDS", exception.Code);
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("ACTIVE")]
    public void EnsureCanUpdateSchedule_AllowsDraftAndActiveYears(string status)
    {
        var year = CreateYear(status);

        year.EnsureCanUpdateSchedule(
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));
    }

    [Fact]
    public void EnsureCanConfigureTerms_RejectsAClosedAcademicYear()
    {
        var year = CreateYear("CLOSED");

        var exception = Assert.Throws<AcademicCalendarDomainException>(year.EnsureCanConfigureTerms);

        Assert.Equal("ACADEMIC_YEAR_CLOSED", exception.Code);
    }

    [Fact]
    public void EnsureCanConfigureTerm_RejectsAClosedSemester()
    {
        var year = CreateYear("ACTIVE");
        year.Semesters.Add(new Semester { Order = 1, Status = "CLOSED" });

        var exception = Assert.Throws<AcademicCalendarDomainException>(() =>
            year.EnsureCanConfigureTerm(1));

        Assert.Equal("TERM_CLOSED", exception.Code);
    }

    private static AcademicYear CreateYear(string status) => new()
    {
        Id = 1,
        Name = "2026-2027",
        StartDate = new DateOnly(2026, 8, 15),
        EndDate = new DateOnly(2027, 5, 31),
        Status = status
    };
}
