using Application.DTOs;
using Application.Validators;
using Domain.Entities.Identity;
using Xunit;

namespace Application.Tests;

public sealed class TeacherAcademicYearFilterTests
{
    [Fact]
    public void OmittedYearRetainsTheExistingTeacherDirectory() =>
        Assert.Null(TeacherValidator.Filter(new TeacherListQuery()).AcademicYearId);

    [Fact]
    public void SelectedYearIsPassedToTheRepositoryFilter() =>
        Assert.Equal(11ul, TeacherValidator.Filter(new TeacherListQuery(AcademicYearId: 11)).AcademicYearId);

    [Fact]
    public void ZeroYearIsRejected() => Assert.Throws<TeacherManagementException>(() =>
        TeacherValidator.Filter(new TeacherListQuery(AcademicYearId: 0)));
}
