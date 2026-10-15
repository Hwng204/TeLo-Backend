using Domain.Entities.Academic;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearWithoutTermsTests
{
    [Fact]
    public void DraftYearCanActivateWithoutAnySemester()
    {
        var year = new AcademicYear { Name = "2026-2027", Status = "DRAFT" };
        year.Activate();
        Assert.Equal("ACTIVE", year.Status);
        Assert.Empty(year.Semesters);
    }

    [Fact]
    public void ExistingSemesterRowsDoNotControlYearLifecycle()
    {
        var year = new AcademicYear { Name = "2026-2027", Status = "DRAFT" };
        year.Semesters.Add(new Semester { Order = 1, Name = "" });
        year.Activate();
        year.Close();
        Assert.Equal("CLOSED", year.Status);
        Assert.Single(year.Semesters);
        Assert.Throws<AcademicCalendarDomainException>(year.Activate);
    }
}
