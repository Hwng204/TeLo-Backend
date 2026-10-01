using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearValidatorTests
{
    [Fact]
    public void ValidateConfigureTerms_UsesRequestIndexWhenTermsAreReversed()
    {
        var request = new ConfigureTermsRequest(
        [
            new(2, "II", new DateOnly(2027, 1, 15), new DateOnly(2027, 5, 31)),
            new(1, "", new DateOnly(2026, 8, 15), new DateOnly(2027, 1, 15))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request,
            new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.Contains("terms[1].name", result.Errors.Keys);
        Assert.Contains("terms[0].startDate", result.Errors.Keys);
        Assert.DoesNotContain("terms[0].name", result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReturnsNameErrorForMissingName(string? name)
    {
        var request = new CreateAcademicYearRequest(
            name!, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        var result = AcademicYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void Validate_ReturnsNameErrorForNonAsciiYearDigits()
    {
        var request = new CreateAcademicYearRequest(
            "٢٠٢٦-٢٠٢٧", new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        var result = AcademicYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void Validate_RejectsMissingDates()
    {
        var request = new CreateAcademicYearRequest("2026-2027", default, default);

        var result = AcademicYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("startDate", result.Errors.Keys);
        Assert.Contains("endDate", result.Errors.Keys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateConfigureTerms_RejectsMissingTerms(bool useNull)
    {
        var request = new ConfigureTermsRequest(useNull ? null! : []);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("terms", result.Errors.Keys);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsNullTermElementWithoutThrowing()
    {
        var request = new ConfigureTermsRequest(
            [null!, new ConfigureTermItem(2, "Term II", null, null)]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("terms", result.Errors.Keys);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void ValidateConfigureTerms_RejectsDuplicateOrInvalidOrders(byte firstOrder, byte secondOrder)
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(firstOrder, "Term I", null, null),
            new ConfigureTermItem(secondOrder, "Term II", null, null)
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("terms", result.Errors.Keys);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public void ValidateConfigureTerms_RejectsTermEndingOnOrBeforeItsStart(byte order, int endOffset)
    {
        var firstStart = new DateOnly(2026, 9, 1);
        var secondStart = new DateOnly(2027, 1, 16);
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Term I", firstStart,
                order == 1 ? firstStart.AddDays(endOffset) : new DateOnly(2027, 1, 15)),
            new ConfigureTermItem(2, "Term II", secondStart,
                order == 2 ? secondStart.AddDays(endOffset) : new DateOnly(2027, 5, 31))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains($"terms[{order - 1}].endDate", result.Errors.Keys);
    }

    [Fact]
    public void Validate_AcceptsAValidAcademicYear()
    {
        var input = new CreateAcademicYearRequest(
            "2026-2027",
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        var result = AcademicYearValidator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidateConfigureTerms_RequiresBothDatesForEveryTerm(byte order)
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Term I",
                order == 1 ? null : new DateOnly(2026, 9, 1),
                order == 1 ? null : new DateOnly(2027, 1, 15)),
            new ConfigureTermItem(2, "Term II",
                order == 2 ? null : new DateOnly(2027, 1, 16),
                order == 2 ? null : new DateOnly(2027, 5, 31))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request, new DateOnly(2026, 8, 15), new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains($"terms[{order - 1}].startDate", result.Errors.Keys);
        Assert.Contains($"terms[{order - 1}].endDate", result.Errors.Keys);
    }

    [Theory]
    [InlineData("", "Tên năm học là bắt buộc.")]
    [InlineData("2026/2027", "Tên năm học phải có định dạng YYYY-YYYY.")]
    [InlineData("2026-2028", "Năm kết thúc phải ngay sau năm bắt đầu.")]
    public void Validate_RejectsInvalidNames(string name, string expectedMessage)
    {
        var input = new CreateAcademicYearRequest(
            name,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        var result = AcademicYearValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains(expectedMessage, result.Errors["name"]);
    }

    [Theory]
    [InlineData(2026, 8, 15, 2026, 8, 15)]
    [InlineData(2027, 5, 31, 2026, 8, 15)]
    public void Validate_RequiresEndDateAfterStartDate(
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay)
    {
        var input = new CreateAcademicYearRequest(
            "2026-2027",
            new DateOnly(startYear, startMonth, startDay),
            new DateOnly(endYear, endMonth, endDay));

        var result = AcademicYearValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Ngày kết thúc phải sau ngày bắt đầu.", result.Errors["endDate"]);
    }

    [Fact]
    public void Validate_RequiresDatesToMatchTheNamedYears()
    {
        var input = new CreateAcademicYearRequest(
            "2026-2027",
            new DateOnly(2025, 8, 15),
            new DateOnly(2027, 5, 31));

        var result = AcademicYearValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Ngày bắt đầu phải thuộc năm 2026.", result.Errors["startDate"]);
    }

    [Fact]
    public void Validate_RejectsAcademicYearLessThan180Days()
    {
        var input = new CreateAcademicYearRequest(
            "2026-2027",
            new DateOnly(2026, 12, 30),
            new DateOnly(2027, 1, 15));

        var result = AcademicYearValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Năm học phải kéo dài tối thiểu 180 ngày.", result.Errors["endDate"]);
    }

    [Theory]
    [InlineData("1999-2000", 1999, 2000)]
    [InlineData("2101-2102", 2101, 2102)]
    public void Validate_RejectsYearsOutside2000To2100(string name, int startYear, int endYear)
    {
        var input = new CreateAcademicYearRequest(
            name,
            new DateOnly(startYear, 8, 15),
            new DateOnly(endYear, 5, 31));

        var result = AcademicYearValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains("Năm học phải nằm trong khoảng từ năm 2000 đến 2100.", result.Errors["name"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsEmptyTermName()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 15)),
            new ConfigureTermItem(2, "Kỳ 2", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 31))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Tên học kỳ 1 không được để trống.", result.Errors["terms[0].name"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsIdenticalNames()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Học kỳ", null, null),
            new ConfigureTermItem(2, "Học kỳ", null, null)
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Tên hai học kỳ không được trùng nhau.", result.Errors["terms[1].name"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsIncompleteDatesForSingleTerm()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Kỳ 1", new DateOnly(2026, 9, 1), null),
            new ConfigureTermItem(2, "Kỳ 2", null, null)
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Vui lòng nhập ngày kết thúc học kỳ 1.", result.Errors["terms[0].endDate"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsDatesOutsideAcademicYear()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Kỳ 1", new DateOnly(2026, 8, 1), new DateOnly(2027, 1, 15)),
            new ConfigureTermItem(2, "Kỳ 2", new DateOnly(2027, 1, 16), new DateOnly(2027, 6, 15))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Ngày bắt đầu học kỳ 1 phải nằm trong khoảng thời gian năm học.", result.Errors["terms[0].startDate"]);
        Assert.Contains("Ngày kết thúc học kỳ 2 phải nằm trong khoảng thời gian năm học.", result.Errors["terms[1].endDate"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsTerm2StartingBeforeTerm1End()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Kỳ 1", new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 20)),
            new ConfigureTermItem(2, "Kỳ 2", new DateOnly(2027, 1, 15), new DateOnly(2027, 5, 25))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Học kỳ 2 phải bắt đầu sau ngày kết thúc của học kỳ 1.", result.Errors["terms[1].startDate"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsTerm2ConfiguredWithoutTerm1()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Kỳ 1", null, null),
            new ConfigureTermItem(2, "Kỳ 2", new DateOnly(2027, 1, 16), new DateOnly(2027, 5, 25))
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(
            request,
            new DateOnly(2026, 8, 15),
            new DateOnly(2027, 5, 31));

        Assert.False(result.IsValid);
        Assert.Contains("Cần cấu hình thời gian học kỳ 1 trước khi cấu hình học kỳ 2.", result.Errors["terms[0].startDate"]);
    }
}
