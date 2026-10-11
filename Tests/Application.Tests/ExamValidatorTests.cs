using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class ExamValidatorTests
{
    [Fact]
    public void CreateExam_RequiresEveryFieldAndValidDateRange()
    {
        var result = ExamValidator.Validate(new CreateExamRequest(
            0, 0, 0, 0, "", null, null, null, null));

        Assert.False(result.IsValid);
        Assert.Equal(
            ["endDate", "endTime", "gradeLevelId", "name", "schoolBranchId", "semesterId", "startDate", "startTime", "subjectId"],
            result.Errors.Keys.OrderBy(key => key).ToArray());
    }

    [Fact]
    public void CreateExam_TreatsNullNameAsMissing()
    {
        var result = ExamValidator.Validate(new CreateExamRequest(
            1, 2, 5, 6, null!,
            new DateOnly(2026, 11, 1), new TimeOnly(8, 0),
            new DateOnly(2026, 11, 1), new TimeOnly(9, 0)));

        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void CreateExam_RejectsEndAtEqualToStartAt()
    {
        var result = ExamValidator.Validate(new CreateExamRequest(
            1,
            2,
            5,
            6,
            "Kiểm tra",
            new DateOnly(2026, 11, 1),
            new TimeOnly(8, 0),
            new DateOnly(2026, 11, 1),
            new TimeOnly(8, 0)));

        Assert.Contains("endDate", result.Errors.Keys);
    }

    [Fact]
    public void CreateExam_RejectsNameLongerThanMaximum()
    {
        var result = ExamValidator.Validate(new CreateExamRequest(
            1, 2, 5, 6, new string('x', 256),
            new DateOnly(2026, 11, 1), new TimeOnly(8, 0),
            new DateOnly(2026, 11, 1), new TimeOnly(9, 0)));

        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void UpdateExam_RequiresStatus()
    {
        var result = ExamValidator.Validate(new UpdateExamRequest(
            1, 2, 5, 6, "Kiểm tra",
            new DateOnly(2026, 11, 1), new TimeOnly(8, 0),
            new DateOnly(2026, 11, 1), new TimeOnly(9, 0), ""));

        Assert.Contains("status", result.Errors.Keys);
    }

    [Fact]
    public void ExamList_RejectsInvalidRangePagingSortingAndStatus()
    {
        var result = ExamValidator.Validate(new ExamListQuery(
            new string('x', 256),
            null,
            null,
            "UNKNOWN",
            new DateOnly(2026, 12, 2),
            new DateOnly(2026, 12, 1),
            0,
            101,
            "unknown",
            "sideways"));

        Assert.False(result.IsValid);
        Assert.Equal(7, result.Errors.Count);
    }

    [Fact]
    public void ExamList_AllowsOmittedOptionalFilters()
    {
        var result = ExamValidator.Validate(new ExamListQuery(
            null, null, null, null, null, null, 1, 20, null, null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExamList_AllowsOneSidedDateFilter(bool hasFromDate)
    {
        var date = new DateOnly(2026, 10, 12);
        var result = ExamValidator.Validate(new ExamListQuery(
            null,
            null,
            null,
            null,
            hasFromDate ? date : null,
            hasFromDate ? null : date,
            1,
            20,
            null,
            null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ExamList_AllowsValidDateRange()
    {
        var result = ExamValidator.Validate(new ExamListQuery(
            null,
            null,
            null,
            null,
            new DateOnly(2026, 10, 12),
            new DateOnly(2026, 10, 13),
            1,
            20,
            null,
            null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void BatchRooms_RequiresExactlyTwoUniqueRoomsAndValidCapacity()
    {
        var result = ExamRoomValidator.Validate(new CreateExamRoomsRequest(
            1,
            0,
            "PM--",
            [20, 20]));

        Assert.False(result.IsValid);
        Assert.Contains("roomCount", result.Errors.Keys);
        Assert.Contains("candidateLimit", result.Errors.Keys);
        Assert.Contains("codePrefix", result.Errors.Keys);
        Assert.Contains("roomIds", result.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNO")]
    public void BatchRooms_RejectsMissingOrTooLongPrefix(string prefix)
    {
        var result = ExamRoomValidator.Validate(new CreateExamRoomsRequest(2, 25, prefix, [20, 21]));

        Assert.Contains("codePrefix", result.Errors.Keys);
    }

    [Fact]
    public void BatchRooms_TreatsNullCollectionsAndPrefixAsMissing()
    {
        var result = ExamRoomValidator.Validate(new CreateExamRoomsRequest(2, 25, null!, null!));

        Assert.Contains("codePrefix", result.Errors.Keys);
        Assert.Contains("roomIds", result.Errors.Keys);
    }

    [Fact]
    public void BatchRooms_RejectsDuplicateOrZeroRoomIds()
    {
        var duplicate = ExamRoomValidator.Validate(new CreateExamRoomsRequest(2, 25, "PM-", [20, 20]));
        var zero = ExamRoomValidator.Validate(new CreateExamRoomsRequest(2, 25, "PM-", [20, 0]));

        Assert.Contains("roomIds", duplicate.Errors.Keys);
        Assert.Contains("roomIds", zero.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PM 01")]
    [InlineData("PM--01")]
    [InlineData("PM@01")]
    public void ExamRoom_RejectsInvalidCode(string code)
    {
        var result = ExamRoomValidator.Validate(new CreateExamRoomRequest(code, 20, 25));

        Assert.Contains("code", result.Errors.Keys);
    }

    [Fact]
    public void ExamRoom_RejectsTooLongCodeMissingRoomAndCapacity()
    {
        var result = ExamRoomValidator.Validate(new UpdateExamRoomRequest(new string('P', 51), 0, 0));

        Assert.Equal(["candidateLimit", "code", "roomId"], result.Errors.Keys.OrderBy(key => key).ToArray());
    }

    [Fact]
    public void ExamRoom_TreatsNullCodeAsMissing()
    {
        var result = ExamRoomValidator.Validate(new CreateExamRoomRequest(null!, 20, 25));

        Assert.Contains("code", result.Errors.Keys);
    }
}
