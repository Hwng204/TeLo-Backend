using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public static partial class IdentityManagementValidator
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{1,99}$")]
    private static partial Regex CodePattern();

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

    private static Dictionary<string, string[]> Fields(string? code, string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CodePattern().IsMatch(code?.Trim() ?? "")) errors["code"] = ["Mã gồm 2–100 chữ cái, số, dấu gạch ngang hoặc gạch dưới; bắt đầu bằng chữ hoặc số."];
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150) errors["name"] = ["Tên bắt buộc, tối đa 150 ký tự."];
        if (description?.Trim().Length > 500) errors["description"] = ["Mô tả tối đa 500 ký tự."];
        return errors;
    }
}
