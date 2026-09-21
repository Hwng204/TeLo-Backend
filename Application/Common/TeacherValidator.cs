using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Application.DTOs;
using Domain.Entities.Identity;
using Infrastructure.Repositories.Interface;

namespace Application.Common;

public static partial class TeacherValidator
{
    public static readonly string[] EmploymentStatuses = ["WORKING", "ON_LEAVE", "RESIGNED", "INACTIVE"];
    public static readonly string[] AccountStatuses = ["ACTIVE", "LOCKED", "INACTIVE"];

    public static TeacherProfileValues Profile(TeacherProfileRequest request, DateOnly today)
    {
        if (request is null) throw Invalid("Hồ sơ giáo viên là bắt buộc.");
        var code = Required(request.StaffCode, 64, "Mã cán bộ").ToUpperInvariant();
        if (!StaffCodePattern().IsMatch(code)) throw Invalid("Mã cán bộ chỉ gồm chữ Latin, số, dấu gạch ngang và gạch dưới.");
        var status = Required(request.EmploymentStatus, 32, "Trạng thái công tác").ToUpperInvariant();
        if (!EmploymentStatuses.Contains(status)) throw Invalid("Trạng thái công tác không hợp lệ.");
        if (request.MainSubjectId == 0) throw Invalid("Môn dạy chính không hợp lệ.");
        if (request.DateOfBirth is { } birth && (birth >= today || birth.Year < 1900))
            throw Invalid("Ngày sinh phải từ năm 1900 và trước ngày hiện tại.");
        if (request.JoinedOn is { } joined && (joined.Year < 1900 || joined > today ||
                (request.DateOfBirth is { } dob && joined <= dob)))
            throw Invalid("Ngày vào trường phải sau ngày sinh và không vượt quá ngày hiện tại.");
        var email = Optional(request.WorkEmail, 254, "Email công tác");
        if (email != null) email = Email(email);
        var phone = Optional(request.Phone, 30, "Số điện thoại");
        if (phone != null && (!PhonePattern().IsMatch(phone) || phone.Count(char.IsAsciiDigit) is < 7 or > 15))
            throw Invalid("Số điện thoại phải có từ 7 đến 15 chữ số.");
        return new(code, Required(request.FullName, 255, "Họ tên"),
            Optional(request.Department, 150, "Tổ chuyên môn"), request.MainSubjectId,
            Optional(request.Specialization, 255, "Chuyên môn"), Optional(request.Position, 150, "Chức vụ"),
            request.Gender, phone, email, request.DateOfBirth, request.JoinedOn, status);
    }

    public static TeacherFilter Filter(TeacherListQuery query)
    {
        Page(query.Page, query.PageSize);
        if (query.MainSubjectId == 0 || query.SchoolBranchId == 0) throw Invalid("Mã môn/phân hiệu không hợp lệ.");
        var employment = Optional(query.EmploymentStatus, 32, "Trạng thái công tác")?.ToUpperInvariant();
        var account = Optional(query.AccountStatus, 32, "Trạng thái tài khoản")?.ToUpperInvariant();
        if (employment != null && !EmploymentStatuses.Contains(employment)) throw Invalid("Trạng thái công tác không hợp lệ.");
        if (account != null && !AccountStatuses.Contains(account)) throw Invalid("Trạng thái tài khoản không hợp lệ.");
        if (query.SortBy is not ("fullName" or "staffCode" or "joinedOn" or "createdAt"))
            throw Invalid("sortBy phải là fullName, staffCode, joinedOn hoặc createdAt.");
        if (query.SortDirection is not ("asc" or "desc")) throw Invalid("sortDirection phải là asc hoặc desc.");
        return new(Optional(query.Search, 200, "Từ khóa"), Optional(query.Department, 150, "Tổ chuyên môn"),
            query.MainSubjectId, employment, account, query.Gender, query.SchoolBranchId,
            query.SortBy, query.SortDirection == "desc", query.Page, query.PageSize);
    }

    public static void Page(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw Invalid("page phải từ 1; pageSize từ 1 đến 100; vị trí trang không vượt giới hạn số nguyên.");
    }

    public static string Username(string value)
    {
        var result = Required(value, 100, "Tên đăng nhập");
        if (!UsernamePattern().IsMatch(result)) throw Invalid("Tên đăng nhập dài 3–100 ký tự, chỉ gồm chữ Latin, số, dấu chấm, gạch ngang và gạch dưới.");
        return result;
    }

    public static string Email(string value)
    {
        var result = Required(value, 254, "Email").ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(result)) throw Invalid("Email không hợp lệ.");
        return result;
    }

    public static void Password(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 12 or > 128 ||
            !value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit) ||
            !value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            throw Invalid("Mật khẩu phải dài 12–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.");
    }

    public static string? Optional(string? value, int max, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max || trimmed.Any(char.IsControl)) throw Invalid($"{label} tối đa {max} ký tự và không chứa ký tự điều khiển.");
        return trimmed;
    }

    private static string Required(string? value, int max, string label) =>
        Optional(value, max, label) ?? throw Invalid($"{label} là bắt buộc.");
    private static TeacherManagementException Invalid(string message) => new("VALIDATION_ERROR", message);
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex StaffCodePattern();
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]{2,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
    [GeneratedRegex("^\\+?[0-9 ()-]{7,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
