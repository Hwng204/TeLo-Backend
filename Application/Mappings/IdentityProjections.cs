using System.Linq.Expressions;
using Application.DTOs;
using Domain.Entities.Identity;

namespace Application.Mappings;

// EF-translatable projections shared by RoleService and UserService.
public static class IdentityProjections
{
    public static readonly Expression<Func<Role, RoleItem>> Role = r => new RoleItem(
        r.Id, r.Code, r.Name, r.Description, r.Status, r.SchoolId, r.School == null ? null : r.School.Name,
        r.SchoolBranchId, r.SchoolBranch == null ? null : r.SchoolBranch.Name, r.IsSystem, r.Version,
        r.UserRoles.Count, !r.IsSystem && r.UsedAt == null && !r.UserRoles.Any() && !r.Permissions.Any());

    public static readonly Expression<Func<User, IdentityUserItem>> User = u => new IdentityUserItem(
        u.Id, u.Username, u.FullName, u.Email, u.Status, u.SchoolBranch == null ? null : u.SchoolBranch.SchoolId,
        u.SchoolBranch == null ? null : u.SchoolBranch.School.Name, u.SchoolBranchId,
        u.SchoolBranch == null ? null : u.SchoolBranch.Name, u.SecurityVersion);
}
