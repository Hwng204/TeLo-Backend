using Domain.Entities.Academic;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearDomainTests
{
    [Fact]
    public void Activate_RejectsYearWithoutTwoTerms()
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester { Order = 1, Name = "Học kỳ I" });
        var error = Assert.Throws<AcademicCalendarDomainException>(year.Activate);
        Assert.Equal("INCOMPLETE_TERMS", error.Code);
        Assert.Equal("DRAFT", year.Status);
    }

    [Theory]
    [InlineData("Học kỳ I", "")]
    [InlineData("Học kỳ", " học kỳ ")]
    public void Activate_RejectsEmptyOrDuplicateTermNames(string first, string second)
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester { Order = 1, Name = first });
        year.Semesters.Add(new Semester { Order = 2, Name = second });
        var error = Assert.Throws<AcademicCalendarDomainException>(year.Activate);
        Assert.Equal("INCOMPLETE_TERMS", error.Code);
    }

    [Fact]
    public void Activate_SetsOnlyTheYearStatus()
    {
        var year = NamedYear();
        year.Activate();
        Assert.Equal("ACTIVE", year.Status);
    }

    [Fact]
    public void Close_RejectsDraftYear()
    {
        var year = NamedYear();
        var error = Assert.Throws<AcademicCalendarDomainException>(year.Close);
        Assert.Equal("ACADEMIC_YEAR_NOT_ACTIVE", error.Code);
    }

    [Fact]
    public void Close_ClosesAnActiveYear()
    {
        var year = NamedYear();
        year.Activate();
        year.Close();
        Assert.Equal("CLOSED", year.Status);
    }

    [Fact]
    public void Activate_RejectsAClosedAcademicYear()
    {
        var year = CreateYear("CLOSED");

        var exception = Assert.Throws<AcademicCalendarDomainException>(year.Activate);

        Assert.Equal("ACADEMIC_YEAR_CLOSED", exception.Code);
    }

    [Fact]
    public void UpdateSchedule_RenamesOpenYearAndRejectsClosedYear()
    {
        var year = CreateYear("ACTIVE");
        year.UpdateSchedule("2027-2028");
        Assert.Equal("2027-2028", year.Name);

        var closed = CreateYear("CLOSED");
        var exception = Assert.Throws<AcademicCalendarDomainException>(() => closed.UpdateSchedule("2027-2028"));
        Assert.Equal("ACADEMIC_YEAR_CLOSED", exception.Code);
    }

    [Fact]
    public void EnsureCanConfigureTerms_RejectsAClosedAcademicYear()
    {
        var year = CreateYear("CLOSED");

        var exception = Assert.Throws<AcademicCalendarDomainException>(year.EnsureCanConfigureTerms);

        Assert.Equal("ACADEMIC_YEAR_CLOSED", exception.Code);
    }

    [Fact]
    public void ConfigureTerm_RenamesTermAndRejectsMissingTerm()
    {
        var year = NamedYear();
        year.ConfigureTerm(2, "Kỳ hai");
        Assert.Equal("Kỳ hai", year.Semesters.Single(term => term.Order == 2).Name);

        var exception = Assert.Throws<AcademicCalendarDomainException>(() => CreateYear("DRAFT").ConfigureTerm(1, "Kỳ một"));
        Assert.Equal("TERM_NOT_FOUND", exception.Code);
    }

    private static AcademicYear NamedYear()
    {
        var year = CreateYear("DRAFT");
        year.Semesters.Add(new Semester { Id = 10, Order = 1, Name = "Học kỳ I" });
        year.Semesters.Add(new Semester { Id = 11, Order = 2, Name = "Học kỳ II" });
        return year;
    }

    private static AcademicYear CreateYear(string status) => new()
    {
        Id = 1,
        Name = "2026-2027",
        Status = status
    };
}
