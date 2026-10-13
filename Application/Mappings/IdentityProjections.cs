using System.Linq.Expressions;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.Notification;

namespace Application.Mappings;

// EF-translatable projections shared by RoleService and UserService.
public static class IdentityProjections
{
    public static Expression<Func<Role, RoleItem>> Role(IQueryable<NotificationTarget> notificationTargets) => r => new RoleItem(
        r.Id, r.Code, r.Name, r.Description, r.Status, r.SchoolId, r.School == null ? null : r.School.Name,
        r.SchoolBranchId, r.SchoolBranch == null ? null : r.SchoolBranch.Name, r.IsSystem, r.Version,
        r.UserRoles.Count,
        !r.IsSystem && r.Code.ToUpper() != "ADMIN" && r.Code.ToUpper() != "OPERATIONALADMIN" &&
        r.UsedAt == null && !r.UserRoles.Any() && !r.Permissions.Any() && !notificationTargets.Any(t => t.RoleId == r.Id),
        r.IsSystem || r.Code.ToUpper() == "ADMIN" || r.Code.ToUpper() == "OPERATIONALADMIN" ? "Vai trò hệ thống được bảo vệ, không thể xóa." :
        r.UserRoles.Any() ? "Vai trò đang được gán cho người dùng. Hãy ngừng áp dụng để giữ lịch sử." :
        r.Permissions.Any() ? "Vai trò đã được cấu hình quyền. Hãy ngừng áp dụng để giữ lịch sử." :
        r.UsedAt != null ? "Vai trò đã có lịch sử sử dụng. Hãy ngừng áp dụng để giữ lịch sử." :
        notificationTargets.Any(t => t.RoleId == r.Id) ? "Vai trò đang được dùng trong cấu hình thông báo. Gỡ liên kết này trước khi xóa." : null);

    public static readonly Expression<Func<User, IdentityUserItem>> User = u => new IdentityUserItem(
        u.Id, u.Username, u.FullName, u.Email, u.Status, u.SchoolBranch == null ? null : u.SchoolBranch.SchoolId,
        u.SchoolBranch == null ? null : u.SchoolBranch.School.Name, u.SchoolBranchId,
        u.SchoolBranch == null ? null : u.SchoolBranch.Name, u.SecurityVersion);
}
