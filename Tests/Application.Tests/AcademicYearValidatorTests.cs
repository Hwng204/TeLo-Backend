using Application.DTOs;
using Application.Validators;
using Xunit;

namespace Application.Tests;

public sealed class AcademicYearValidatorTests
{
    [Fact]
    public void ValidateConfigureTerms_UsesRequestIndexWhenTermsAreReversed()
    {
        var request = new ConfigureTermsRequest(
        [
            new(2, "II"),
            new(1, "")
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.Contains("terms[1].name", result.Errors.Keys);
        Assert.DoesNotContain("terms[0].name", result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReturnsNameErrorForMissingName(string? name)
    {
        var request = new CreateAcademicYearRequest(name!);

        var result = AcademicYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void Validate_ReturnsNameErrorForNonAsciiYearDigits()
    {
        var request = new CreateAcademicYearRequest("٢٠٢٦-٢٠٢٧");

        var result = AcademicYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateConfigureTerms_RejectsMissingTerms(bool useNull)
    {
        var request = new ConfigureTermsRequest(useNull ? null! : []);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.False(result.IsValid);
        Assert.Contains("terms", result.Errors.Keys);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsNullTermElementWithoutThrowing()
    {
        var request = new ConfigureTermsRequest(
            [null!, new ConfigureTermItem(2, "Term II")]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

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
            new ConfigureTermItem(firstOrder, "Term I"),
            new ConfigureTermItem(secondOrder, "Term II")
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.False(result.IsValid);
        Assert.Contains("terms", result.Errors.Keys);
    }

    [Fact]
    public void Validate_AcceptsAValidAcademicYear()
    {
        var result = AcademicYearValidator.Validate(new CreateAcademicYearRequest("2026-2027"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateConfigureTerms_AcceptsTwoDistinctNames()
    {
        var request = new ConfigureTermsRequest([new(1, "Kỳ 1"), new(2, "Kỳ 2")]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Tên năm học là bắt buộc.")]
    [InlineData("2026/2027", "Tên năm học phải có định dạng YYYY-YYYY.")]
    [InlineData("2026-2028", "Năm kết thúc phải ngay sau năm bắt đầu.")]
    public void Validate_RejectsInvalidNames(string name, string expectedMessage)
    {
        var result = AcademicYearValidator.Validate(new CreateAcademicYearRequest(name));

        Assert.False(result.IsValid);
        Assert.Contains(expectedMessage, result.Errors["name"]);
    }

    [Theory]
    [InlineData("1999-2000")]
    [InlineData("2101-2102")]
    public void Validate_RejectsYearsOutside2000To2100(string name)
    {
        var result = AcademicYearValidator.Validate(new CreateAcademicYearRequest(name));

        Assert.False(result.IsValid);
        Assert.Contains("Năm học phải nằm trong khoảng từ năm 2000 đến 2100.", result.Errors["name"]);
    }

    [Fact]
    public void ValidateUpdate_AppliesTheSameNameRules()
    {
        var result = AcademicYearValidator.ValidateUpdate(new UpdateAcademicYearRequest("2026/2027"));

        Assert.False(result.IsValid);
        Assert.Contains("name", result.Errors.Keys);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsEmptyTermName()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, ""),
            new ConfigureTermItem(2, "Kỳ 2")
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.False(result.IsValid);
        Assert.Contains("Tên học kỳ 1 không được để trống.", result.Errors["terms[0].name"]);
    }

    [Fact]
    public void ValidateConfigureTerms_RejectsIdenticalNames()
    {
        var request = new ConfigureTermsRequest(
        [
            new ConfigureTermItem(1, "Học kỳ"),
            new ConfigureTermItem(2, "Học kỳ")
        ]);

        var result = AcademicYearValidator.ValidateConfigureTerms(request);

        Assert.False(result.IsValid);
        Assert.Contains("Tên hai học kỳ không được trùng nhau.", result.Errors["terms[1].name"]);
    }
}
