using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public static partial class IdentityManagementValidator
{
    public static readonly IReadOnlySet<string> UserStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "ACTIVE",
        "INACTIVE",
        "LOCKED"
    };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{1,99}$")]
    private static partial Regex CodePattern();
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]{2,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    public static Dictionary<string, string[]> Query(IdentityListQuery query, bool allowLockedAccount = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1 || query.Page > 1_000_000) errors["page"] = ["Trang phải từ 1 đến 1.000.000."];
        if (query.PageSize < 1 || query.PageSize > 100) errors["pageSize"] = ["Số dòng mỗi trang phải từ 1 đến 100."];
        if (query.Search?.Length > 150) errors["search"] = ["Từ khóa tối đa 150 ký tự."];
        if (!string.IsNullOrEmpty(query.Status) && query.Status is not ("ACTIVE" or "INACTIVE") && !(allowLockedAccount && query.Status == "LOCKED")) errors["status"] = ["Trạng thái không hợp lệ."];
        if (query.SchoolId == 0 || query.SchoolBranchId == 0 || query.RoleId == 0 || query.EligibleForRoleId == 0)
            errors["scope"] = ["Mã định danh phải lớn hơn 0."];
        return errors;
    }

    public static Dictionary<string, string[]> Role(SaveRoleRequest request)
    {
        var errors = Fields(request.Code, request.Name, request.Description);
        if (request.SchoolBranchId.HasValue && !request.SchoolId.HasValue) errors["schoolId"] = ["Chọn trường trước khi chọn phân hiệu."];
        if (request.SchoolId == 0) errors["schoolId"] = ["Trường không hợp lệ."];
        if (request.SchoolBranchId == 0) errors["schoolBranchId"] = ["Phân hiệu không hợp lệ."];
        return errors;
    }

    public static Dictionary<string, string[]> Module(SaveModuleRequest request) => Fields(request.Code, request.Name, request.Description);

    public static Dictionary<string, string[]> CreateUser(CreateUserRequest request)
    {
        var errors = UserFields(request.Username, request.Email, request.FullName, request.MoetIdentifier, request.SchoolBranchId);
        Password(request.Password, "password", errors);
        if (!UserStatuses.Contains(request.Status?.Trim().ToUpperInvariant() ?? "")) errors["status"] = ["Trạng thái không hợp lệ."];
        if (request.RoleIds != null && (request.RoleIds.Length > 100 || request.RoleIds.Any(x => x == 0) || request.RoleIds.Distinct().Count() != request.RoleIds.Length))
            errors["roleIds"] = ["Chọn tối đa 100 vai trò khác nhau."];
        return errors;
    }

    public static Dictionary<string, string[]> UpdateUser(UpdateUserRequest request) =>
        UserFields(request.Username, request.Email, request.FullName, request.MoetIdentifier, request.SchoolBranchId);

    public static Dictionary<string, string[]> UserStatus(IdentityStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!UserStatuses.Contains(NormalizeStatus(request.Status)))
            errors["status"] = ["Trạng thái không hợp lệ."];
        return errors;
    }

    public static Dictionary<string, string[]> ResetPassword(ResetUserPasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Password(request.NewPassword, "newPassword", errors);
        return errors;
    }

    private static Dictionary<string, string[]> UserFields(string? username, string? email, string? fullName, string? moetIdentifier, ulong? schoolBranchId)
    {
        var errors = new Dictionary<string, string[]>();
        if (!UsernamePattern().IsMatch(username?.Trim() ?? ""))
            errors["username"] = ["Tên đăng nhập dài 3–100 ký tự, chỉ gồm chữ Latin, số, dấu chấm, gạch ngang và gạch dưới."];
        var mail = email?.Trim();
        if (string.IsNullOrEmpty(mail) || mail.Length > 254 || !new EmailAddressAttribute().IsValid(mail)) errors["email"] = ["Email bắt buộc, hợp lệ, tối đa 254 ký tự."];
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 255 || fullName.Any(char.IsControl)) errors["fullName"] = ["Họ tên bắt buộc, tối đa 255 ký tự."];
        var normalizedMoetIdentifier = moetIdentifier?.Trim();
        if (normalizedMoetIdentifier?.Length > 100 || normalizedMoetIdentifier?.Any(char.IsControl) == true)
            errors["moetIdentifier"] = ["Mã định danh Bộ GD tối đa 100 ký tự và không chứa ký tự điều khiển."];
        if (schoolBranchId == 0) errors["schoolBranchId"] = ["Phân hiệu không hợp lệ."];
        return errors;
    }

    private static void Password(string? value, string key, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 12 or > 128 ||
            !value.Any(char.IsUpper) || !value.Any(char.IsLower) || !value.Any(char.IsDigit) ||
            !value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            errors[key] = ["Mật khẩu phải dài 12–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt."];
    }

    public static string NormalizeStatus(string? status) => status?.Trim().ToUpperInvariant() ?? string.Empty;

    private static Dictionary<string, string[]> Fields(string? code, string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CodePattern().IsMatch(code?.Trim() ?? "")) errors["code"] = ["Mã gồm 2–100 chữ cái, số, dấu gạch ngang hoặc gạch dưới; bắt đầu bằng chữ hoặc số."];
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150) errors["name"] = ["Tên bắt buộc, tối đa 150 ký tự."];
        if (description?.Trim().Length > 500) errors["description"] = ["Mô tả tối đa 500 ký tự."];
        return errors;
    }
}
