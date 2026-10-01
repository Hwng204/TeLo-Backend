using Domain.Entities.Academic;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearDomainTests
{
    [Fact]
    public void Activate_RejectsTwoTermsWithoutDates()
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester { Order = 1, Name = "Học kỳ I" });
        year.Semesters.Add(new Semester { Order = 2, Name = "Học kỳ II" });
        var error = Assert.Throws<AcademicCalendarDomainException>(year.Activate);
        Assert.Equal("INCOMPLETE_TERMS", error.Code);
        Assert.Equal("DRAFT", year.Status);
    }

    [Fact]
    public void Activate_RejectsOverlappingTerms()
    {
        var year = ScheduledYear();
        year.Semesters.Last().StartDate = new DateOnly(2027, 1, 15);
        Assert.Throws<AcademicCalendarDomainException>(year.Activate);
    }

    [Fact]
    public void ActivateAndCloseFirstTerm_AdvancesToSecondTerm()
    {
        var year = ScheduledYear();
        year.Activate();
        Assert.Equal("ACTIVE", year.Semesters.First().Status);
        Assert.Equal("PLANNED", year.Semesters.Last().Status);
        var previousVersion = year.Version;
        year.CloseTerm(10);
        Assert.Equal("CLOSED", year.Semesters.First().Status);
        Assert.Equal("ACTIVE", year.Semesters.Last().Status);
        Assert.True(year.Version > previousVersion);
    }

    [Fact]
    public void Close_RejectsDraftYear()
    {
        var year = ScheduledYear();
        Assert.Throws<AcademicCalendarDomainException>(year.Close);
    }

    [Fact]
    public void CloseTerm_RejectsClosingSecondTermBeforeFirst()
    {
        var year = ScheduledYear();
        year.Activate();
        Assert.Throws<AcademicCalendarDomainException>(() => year.CloseTerm(11));
    }

    private static AcademicYear ScheduledYear()
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester { Id = 10, Order = 1, Name = "Học kỳ I", StartDate = year.StartDate, EndDate = new DateOnly(2027, 1, 15) });
        year.Semesters.Add(new Semester { Id = 11, Order = 2, Name = "Học kỳ II", StartDate = new DateOnly(2027, 1, 16), EndDate = year.EndDate });
        return year;
    }

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
