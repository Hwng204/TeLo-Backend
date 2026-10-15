using System.Linq.Expressions;
using Application.DTOs;
using Domain.Entities.Identity;

namespace Application.Mappings;

// EF-translatable projections shared by RoleService and UserService.
public static class IdentityProjections
{
    // systemCodes: IdentityManagementRules.SystemRoleCodes (upper case).
    public static Expression<Func<Role, RoleItem>> Role(string[] systemCodes) => r => new RoleItem(
        r.Id, r.Code, r.Name, r.Status, systemCodes.Contains(r.Code.ToUpper()), r.UserRoles.Count,
        !systemCodes.Contains(r.Code.ToUpper()) && !r.UserRoles.Any() && !r.Permissions.Any(),
        systemCodes.Contains(r.Code.ToUpper()) ? "Vai trò hệ thống được bảo vệ, không thể xóa." :
        r.UserRoles.Any() ? "Vai trò đang được gán cho người dùng. Hãy ngừng áp dụng để giữ lịch sử." :
        r.Permissions.Any() ? "Vai trò đã được cấu hình quyền. Hãy ngừng áp dụng để giữ lịch sử." : null);

    // Email lives on the teacher profile only (no user.email column).
    public static readonly Expression<Func<User, IdentityUserItem>> User = u => new IdentityUserItem(
        u.Id, u.Username, u.FullName, u.Teachers.Select(t => t.Email).FirstOrDefault() ?? "", u.Status,
        u.SchoolBranch == null ? null : u.SchoolBranch.SchoolId,
        u.SchoolBranch == null ? null : u.SchoolBranch.School.Name, u.SchoolBranchId,
        u.SchoolBranch == null ? null : u.SchoolBranch.Name, u.SecurityVersion);
}
