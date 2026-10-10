using Application.DTOs;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;

namespace Application.Common;

public sealed class EmailRecipientResolver(ApplicationDbContext db)
{
    public IQueryable<User> EligibleUsers(ulong schoolId) => db.Users.AsNoTracking().Where(u => u.Status == "ACTIVE" &&
        u.SchoolBranch != null && u.SchoolBranch.SchoolId == schoolId && u.SchoolBranch.Status == "ACTIVE" && u.SchoolBranch.School.Status == "ACTIVE");

    public async Task<Dictionary<string, string[]>> ValidateAsync(ulong schoolId, SaveEmailConfigRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.RecipientScope is not ("NONE" or "ALL_SCHOOL")) errors["recipientScope"] = ["Phạm vi người nhận không hợp lệ."];
        if (request.Targets == null || request.Targets.Length > 200 || request.Targets.Any(t => t == null ||
            t.Action is not ("INCLUDE" or "EXCLUDE") || t.RoleId.HasValue == t.UserId.HasValue || t.RoleId == 0 || t.UserId == 0))
        {
            errors["targets"] = ["Chọn tối đa 200 nhóm/cá nhân; mỗi lựa chọn chỉ gồm một vai trò hoặc một tài khoản."];
            return errors;
        }
        if (request.Targets.Select(t => (t.RoleId, t.UserId)).Distinct().Count() != request.Targets.Length)
            errors["targets"] = ["Một nhóm/cá nhân chỉ được xuất hiện một lần trong cấu hình."];
        var roleIds = request.Targets.Where(t => t.RoleId.HasValue).Select(t => t.RoleId!.Value).ToArray();
        var userIds = request.Targets.Where(t => t.UserId.HasValue).Select(t => t.UserId!.Value).ToArray();
        if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id) && r.Status == "ACTIVE" &&
            (r.SchoolId == null || r.SchoolId == schoolId) && (r.SchoolBranchId == null || (r.SchoolBranch!.Status == "ACTIVE" && r.SchoolBranch.SchoolId == schoolId)), ct) != roleIds.Length)
            errors["targets"] = ["Nhóm phải đang hoạt động và thuộc phạm vi trường."];
        if (await EligibleUsers(schoolId).CountAsync(u => userIds.Contains(u.Id), ct) != userIds.Length)
            errors["targets"] = ["Tài khoản phải đang hoạt động và thuộc trường."];
        return errors;
    }

    public IQueryable<User> Resolve(ulong schoolId, SaveEmailConfigRequest request)
    {
        var includeRoles = request.Targets.Where(t => t.Action == "INCLUDE" && t.RoleId.HasValue).Select(t => t.RoleId!.Value).ToArray();
        var excludeRoles = request.Targets.Where(t => t.Action == "EXCLUDE" && t.RoleId.HasValue).Select(t => t.RoleId!.Value).ToArray();
        var includeUsers = request.Targets.Where(t => t.Action == "INCLUDE" && t.UserId.HasValue).Select(t => t.UserId!.Value).ToArray();
        var excludeUsers = request.Targets.Where(t => t.Action == "EXCLUDE" && t.UserId.HasValue).Select(t => t.UserId!.Value).ToArray();
        var includedGrants = db.UserRoles.WhereEffective().Where(r => includeRoles.Contains(r.RoleId)).Select(r => r.UserId);
        var excludedGrants = db.UserRoles.WhereEffective().Where(r => excludeRoles.Contains(r.RoleId)).Select(r => r.UserId);
        return EligibleUsers(schoolId).Where(u =>
            (request.RecipientScope == "ALL_SCHOOL" || includeUsers.Contains(u.Id) || includedGrants.Contains(u.Id)) &&
            !excludeUsers.Contains(u.Id) && !excludedGrants.Contains(u.Id));
    }
}
