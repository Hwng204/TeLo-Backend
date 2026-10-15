using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Validators;

public sealed record AcademicYearValidationResult(
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static partial class AcademicYearValidator
{
    public static AcademicYearValidationResult Validate(CreateAcademicYearRequest request) =>
        ValidateName(request.Name);

    public static AcademicYearValidationResult ValidateUpdate(UpdateAcademicYearRequest request) =>
        ValidateName(request.Name);

    private static AcademicYearValidationResult ValidateName(string? rawName)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var name = rawName?.Trim() ?? string.Empty;
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
        }

        return new AcademicYearValidationResult(
            errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    public static AcademicYearValidationResult ValidateConfigureTerms(ConfigureTermsRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request.Terms is null || request.Terms.Count != 2 || request.Terms.Any(t => t is null))
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

        var term1Index = request.Terms[0].Order == 1 ? 0 : 1;
        var term2Index = 1 - term1Index;

        // Validate names
        if (string.IsNullOrWhiteSpace(term1.Name))
            AddError(errors, $"terms[{term1Index}].name", "Tên học kỳ 1 không được để trống.");
        else if (term1.Name.Trim().Length > 100)
            AddError(errors, $"terms[{term1Index}].name", "Tên học kỳ 1 tối đa 100 ký tự.");

        if (string.IsNullOrWhiteSpace(term2.Name))
            AddError(errors, $"terms[{term2Index}].name", "Tên học kỳ 2 không được để trống.");
        else if (term2.Name.Trim().Length > 100)
            AddError(errors, $"terms[{term2Index}].name", "Tên học kỳ 2 tối đa 100 ký tự.");

        if (!string.IsNullOrWhiteSpace(term1.Name) && !string.IsNullOrWhiteSpace(term2.Name) &&
            string.Equals(term1.Name.Trim(), term2.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            AddError(errors, $"terms[{term2Index}].name", "Tên hai học kỳ không được trùng nhau.");
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

    [GeneratedRegex(@"^([0-9]{4})-([0-9]{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex AcademicYearNamePattern();
}
