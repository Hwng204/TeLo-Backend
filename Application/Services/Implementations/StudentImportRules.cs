using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Application.DTOs;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

internal sealed record ParsedStudentFields(
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly? AdmissionDate);

internal sealed record RowValidation(
    IReadOnlyList<StudentImportRowErrorDto> Errors,
    ulong? ClassId);

// Row rules for the student import. Pure, so preview and apply share the same parsing and the
// rules can be unit-tested without a database.
internal static class StudentImportRules
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 255;

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];
    private static readonly DateOnly EarliestDate = new(1900, 1, 1);
    private static readonly DateOnly LatestDate = new(2100, 12, 31);

    private static readonly JsonSerializerOptions ErrorJson = new(JsonSerializerDefaults.Web)
    {
        // Keep Vietnamese readable instead of \uXXXX escapes, so a row's errors stay short.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Parses and checks the five value columns. Fields that fail are reported by name.
    public static ParsedStudentFields ParseFields(
        string code,
        string fullName,
        string dateOfBirth,
        string gender,
        string admissionDate,
        DateOnly today,
        List<StudentImportRowErrorDto> errors)
    {
        if (code.Length == 0)
        {
            errors.Add(new("code", "Mã học sinh là bắt buộc."));
        }
        else if (code.Length > MaxCodeLength)
        {
            errors.Add(new("code", $"Mã học sinh tối đa {MaxCodeLength} ký tự."));
        }

        if (fullName.Length == 0)
        {
            errors.Add(new("fullName", "Họ và tên là bắt buộc."));
        }
        else if (fullName.Length > MaxNameLength)
        {
            errors.Add(new("fullName", $"Họ và tên tối đa {MaxNameLength} ký tự."));
        }

        DateOnly? dob = null;
        if (dateOfBirth.Length > 0)
        {
            if (!TryParseDate(dateOfBirth, out var parsed))
            {
                errors.Add(new("dateOfBirth", "Ngày sinh không hợp lệ, dùng định dạng dd/MM/yyyy."));
            }
            else if (parsed >= today)
            {
                errors.Add(new("dateOfBirth", "Ngày sinh phải trước ngày hôm nay."));
            }
            else
            {
                dob = parsed;
            }
        }

        string? normalizedGender = null;
        if (gender.Length > 0)
        {
            normalizedGender = NormalizeGender(gender);
            if (normalizedGender is null)
            {
                errors.Add(new("gender", "Giới tính phải là NAM, NU hoặc KHAC."));
            }
        }

        DateOnly? admission = null;
        if (admissionDate.Length == 0)
        {
            errors.Add(new("admissionDate", "Ngày nhập học là bắt buộc."));
        }
        else if (!TryParseDate(admissionDate, out var parsedAdmission))
        {
            errors.Add(new("admissionDate", "Ngày nhập học không hợp lệ, dùng định dạng dd/MM/yyyy."));
        }
        else
        {
            admission = parsedAdmission;
        }

        return new ParsedStudentFields(code, fullName, dob, normalizedGender, admission);
    }

    // Full check of one sheet row against what the database knows.
    public static RowValidation Validate(
        StudentImportRawRow raw,
        ILookup<string, ImportClassRow> classesByCode,
        IReadOnlySet<string> existingCodes,
        Dictionary<string, int> firstRowByCode,
        DateOnly today)
    {
        var errors = new List<StudentImportRowErrorDto>();
        ParseFields(
            raw.Code, raw.FullName, raw.DateOfBirth, raw.Gender, raw.AdmissionDate, today, errors);

        if (raw.Code.Length is > 0 and <= MaxCodeLength)
        {
            if (firstRowByCode.TryGetValue(raw.Code, out var firstRow))
            {
                errors.Add(new("code", $"Mã học sinh trùng với dòng {firstRow} trong file."));
            }
            else
            {
                firstRowByCode[raw.Code] = raw.RowNumber;
                if (existingCodes.Contains(raw.Code))
                {
                    errors.Add(new("code", "Mã học sinh đã tồn tại trong hệ thống."));
                }
            }
        }

        ulong? classId = null;
        if (raw.ClassCode.Length == 0)
        {
            errors.Add(new("classCode", "Mã lớp là bắt buộc."));
        }
        else
        {
            var matches = classesByCode[raw.ClassCode].ToList();
            if (matches.Count == 0)
            {
                errors.Add(new("classCode", "Không tìm thấy mã lớp trong năm học này."));
            }
            else if (matches.Count > 1)
            {
                errors.Add(new("classCode", "Mã lớp trùng giữa nhiều cơ sở, không xác định được lớp."));
            }
            else
            {
                classId = matches[0].Id;
            }
        }

        return new RowValidation(errors, classId);
    }

    public static string? SerializeErrors(IReadOnlyList<StudentImportRowErrorDto> errors)
    {
        if (errors.Count == 0)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(errors, ErrorJson);
        // The column is 2000 wide; a row cannot realistically get near it, but never overflow.
        return json.Length <= 2000 ? json : json[..2000];
    }

    public static IReadOnlyList<StudentImportRowErrorDto> DeserializeErrors(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StudentImportRowErrorDto>>(json, ErrorJson) ?? [];
        }
        catch (JsonException)
        {
            return [new StudentImportRowErrorDto("row", "Không đọc được chi tiết lỗi của dòng này.")];
        }
    }

    public static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
        date >= EarliestDate && date <= LatestDate;

    private static string? NormalizeGender(string value) =>
        value.ToUpperInvariant() switch
        {
            "NAM" => "NAM",
            "NU" or "NỮ" => "NU",
            "KHAC" or "KHÁC" => "KHAC",
            _ => null
        };
}
