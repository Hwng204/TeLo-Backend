using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public sealed record AcademicYearValidationResult(
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static partial class AcademicYearValidator
{
    public static AcademicYearValidationResult Validate(CreateAcademicYearRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var name = request.Name.Trim();
        var nameMatch = AcademicYearNamePattern().Match(name);
        if (name.Length == 0)
        {
            AddError(errors, "name", "Tên năm học là bắt buộc.");
        }
        else if (!nameMatch.Success)
        {
            AddError(errors, "name", "Tên năm học phải có định dạng YYYY-YYYY.");
        }
        else
        {
            var startYear = int.Parse(nameMatch.Groups[1].Value);
            var endYear = int.Parse(nameMatch.Groups[2].Value);

            if (endYear != startYear + 1)
            {
                AddError(errors, "name", "Năm kết thúc phải ngay sau năm bắt đầu.");
            }

            if (startYear < 2000 || startYear > 2100)
            {
                AddError(errors, "name", "Năm học phải nằm trong khoảng từ năm 2000 đến 2100.");
            }

            if (request.StartDate.Year != startYear)
            {
                AddError(errors, "startDate", $"Ngày bắt đầu phải thuộc năm {startYear}.");
            }

            if (request.EndDate.Year != endYear)
            {
                AddError(errors, "endDate", $"Ngày kết thúc phải thuộc năm {endYear}.");
            }
        }

        if (request.EndDate <= request.StartDate)
        {
            AddError(errors, "endDate", "Ngày kết thúc phải sau ngày bắt đầu.");
        }
        else if (request.EndDate.DayNumber - request.StartDate.DayNumber < 180)
        {
            AddError(errors, "endDate", "Năm học phải kéo dài tối thiểu 180 ngày.");
        }

        return new AcademicYearValidationResult(
            errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    public static AcademicYearValidationResult ValidateUpdate(UpdateAcademicYearRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var name = request.Name?.Trim() ?? string.Empty;
        var nameMatch = AcademicYearNamePattern().Match(name);
        if (name.Length == 0)
        {
            AddError(errors, "name", "Tên năm học là bắt buộc.");
        }
        else if (!nameMatch.Success)
        {
            AddError(errors, "name", "Tên năm học phải có định dạng YYYY-YYYY.");
        }
        else
        {
            var startYear = int.Parse(nameMatch.Groups[1].Value);
            var endYear = int.Parse(nameMatch.Groups[2].Value);

            if (endYear != startYear + 1)
            {
                AddError(errors, "name", "Năm kết thúc phải ngay sau năm bắt đầu.");
            }

            if (startYear < 2000 || startYear > 2100)
            {
                AddError(errors, "name", "Năm học phải nằm trong khoảng từ năm 2000 đến 2100.");
            }

            if (request.StartDate.Year != startYear)
            {
                AddError(errors, "startDate", $"Ngày bắt đầu phải thuộc năm {startYear}.");
            }

            if (request.EndDate.Year != endYear)
            {
                AddError(errors, "endDate", $"Ngày kết thúc phải thuộc năm {endYear}.");
            }
        }

        if (request.EndDate <= request.StartDate)
        {
            AddError(errors, "endDate", "Ngày kết thúc phải sau ngày bắt đầu.");
        }
        else if (request.EndDate.DayNumber - request.StartDate.DayNumber < 180)
        {
            AddError(errors, "endDate", "Năm học phải kéo dài tối thiểu 180 ngày.");
        }

        return new AcademicYearValidationResult(
            errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    public static AcademicYearValidationResult ValidateConfigureTerms(
        ConfigureTermsRequest request,
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request.Terms is null || request.Terms.Count != 2)
        {
            AddError(errors, "terms", "Cấu hình năm học phải có đúng 2 học kỳ.");
            return new AcademicYearValidationResult(
                errors.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }

        var term1 = request.Terms.FirstOrDefault(t => t.Order == 1);
        var term2 = request.Terms.FirstOrDefault(t => t.Order == 2);

        if (term1 is null || term2 is null)
        {
            AddError(errors, "terms", "Phải bao gồm đủ Học kỳ 1 (Order 1) và Học kỳ 2 (Order 2).");
            return new AcademicYearValidationResult(
                errors.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }

        // Validate names
        if (string.IsNullOrWhiteSpace(term1.Name))
            AddError(errors, "terms[0].name", "Tên học kỳ 1 không được để trống.");
        else if (term1.Name.Trim().Length > 100)
            AddError(errors, "terms[0].name", "Tên học kỳ 1 tối đa 100 ký tự.");

        if (string.IsNullOrWhiteSpace(term2.Name))
            AddError(errors, "terms[1].name", "Tên học kỳ 2 không được để trống.");
        else if (term2.Name.Trim().Length > 100)
            AddError(errors, "terms[1].name", "Tên học kỳ 2 tối đa 100 ký tự.");

        if (!string.IsNullOrWhiteSpace(term1.Name) && !string.IsNullOrWhiteSpace(term2.Name) &&
            string.Equals(term1.Name.Trim(), term2.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, "terms[1].name", "Tên hai học kỳ không được trùng nhau.");
        }

        // Validate Term 1 dates
        if (term1.StartDate.HasValue != term1.EndDate.HasValue)
        {
            if (!term1.StartDate.HasValue)
                AddError(errors, "terms[0].startDate", "Vui lòng nhập ngày bắt đầu học kỳ 1.");
            if (!term1.EndDate.HasValue)
                AddError(errors, "terms[0].endDate", "Vui lòng nhập ngày kết thúc học kỳ 1.");
        }
        else if (term1.StartDate.HasValue && term1.EndDate.HasValue)
        {
            if (term1.StartDate.Value < yearStart || term1.StartDate.Value > yearEnd)
                AddError(errors, "terms[0].startDate", "Ngày bắt đầu học kỳ 1 phải nằm trong khoảng thời gian năm học.");

            if (term1.EndDate.Value < yearStart || term1.EndDate.Value > yearEnd)
                AddError(errors, "terms[0].endDate", "Ngày kết thúc học kỳ 1 phải nằm trong khoảng thời gian năm học.");

            if (term1.EndDate.Value <= term1.StartDate.Value)
                AddError(errors, "terms[0].endDate", "Ngày kết thúc học kỳ 1 phải sau ngày bắt đầu.");
        }

        // Validate Term 2 dates
        if (term2.StartDate.HasValue != term2.EndDate.HasValue)
        {
            if (!term2.StartDate.HasValue)
                AddError(errors, "terms[1].startDate", "Vui lòng nhập ngày bắt đầu học kỳ 2.");
            if (!term2.EndDate.HasValue)
                AddError(errors, "terms[1].endDate", "Vui lòng nhập ngày kết thúc học kỳ 2.");
        }
        else if (term2.StartDate.HasValue && term2.EndDate.HasValue)
        {
            if (term2.StartDate.Value < yearStart || term2.StartDate.Value > yearEnd)
                AddError(errors, "terms[1].startDate", "Ngày bắt đầu học kỳ 2 phải nằm trong khoảng thời gian năm học.");

            if (term2.EndDate.Value < yearStart || term2.EndDate.Value > yearEnd)
                AddError(errors, "terms[1].endDate", "Ngày kết thúc học kỳ 2 phải nằm trong khoảng thời gian năm học.");

            if (term2.EndDate.Value <= term2.StartDate.Value)
                AddError(errors, "terms[1].endDate", "Ngày kết thúc học kỳ 2 phải sau ngày bắt đầu.");
        }

        // Dependency between Term 1 and Term 2
        if (term2.StartDate.HasValue && !term1.StartDate.HasValue)
        {
            AddError(errors, "terms[0].startDate", "Cần cấu hình thời gian học kỳ 1 trước khi cấu hình học kỳ 2.");
        }

        // Cross-term date check
        if (term1.EndDate.HasValue && term2.StartDate.HasValue && term2.StartDate.Value <= term1.EndDate.Value)
        {
            AddError(errors, "terms[1].startDate", "Học kỳ 2 phải bắt đầu sau ngày kết thúc của học kỳ 1.");
        }

        return new AcademicYearValidationResult(
            errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    [GeneratedRegex(@"^(\d{4})-(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex AcademicYearNamePattern();
}
