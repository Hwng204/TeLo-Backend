using Application.DTOs;
using Application.Validators;
using Xunit;

namespace Application.Tests;

public sealed class SchoolDirectoryValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0ul, false)]
    [InlineData(12ul, true)]
    public void UpdateStudent_AllowsKeepingTheClassButRejectsAnExplicitZeroClass(ulong? classId, bool valid)
    {
        var request = new UpdateStudentRequest("HS-1", "Nguyen An", null, null, "ACTIVE", classId);

        var errors = SchoolDirectoryValidator.ValidateUpdateStudent(request, out _, out _, out _);

        Assert.Equal(valid, errors.Count == 0);
        Assert.Equal(!valid, errors.ContainsKey("schoolClassId"));
    }
}
