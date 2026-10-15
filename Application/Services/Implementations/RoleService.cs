using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using static Application.Common.IdentityManagementRules;

namespace Application.Services.Implement;

// Roles are global (no school/branch scope) and have no version column; `version` arguments are ignored.
public sealed class RoleService(ApplicationDbContext db, IMatrixRoleCatalog roleCatalog) : IRoleService
{
    private Expression<Func<Role, RoleItem>> RoleProjection => IdentityProjections.Role(SystemRoleCodes(roleCatalog));
    private static readonly Expression<Func<User, IdentityUserItem>> UserProjection = IdentityProjections.User;

    public async Task<ServiceResult<DirectoryPage<RoleItem>>> ListAsync(IdentityListQuery query, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<RoleItem>>(errors);
        var rows = db.Roles.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(r => r.Code.Contains(search) || r.Name.Contains(search));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(r => r.Status == query.Status);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(r => r.Name).ThenBy(r => r.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(RoleProjection).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<RoleItem>>.Success(new(items, query.Page, query.PageSize, count));
    }

    public async Task<ServiceResult<RoleItem>> GetAsync(ulong id, CancellationToken ct)
    {
        var item = await db.Roles.AsNoTracking().Where(r => r.Id == id).Select(RoleProjection).SingleOrDefaultAsync(ct);
        return item == null ? Missing<RoleItem>() : ServiceResult<RoleItem>.Success(item);
    }

    public async Task<ServiceResult<RoleItem>> SaveAsync(ulong? id, SaveRoleRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Role(request);
        if (errors.Count > 0) return Invalid<RoleItem>(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<RoleItem>();
        var role = id.HasValue ? await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct) : new Role();
        if (role == null) return Missing<RoleItem>();
        if (id.HasValue && !string.Equals(role.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            return Invalid<RoleItem>(new Dictionary<string, string[]> { ["code"] = ["Không được thay đổi mã vai trò."] });
        if (!id.HasValue && IsAdministrator(request.Code.Trim())) return Conflict<RoleItem>("Không được tạo thêm mã quản trị hệ thống.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.Code == code && (!id.HasValue || r.Id != id.Value), ct)) return Conflict<RoleItem>("Mã vai trò đã tồn tại.");
        var before = Snapshot(role);
        if (!id.HasValue) { role.Code = code; db.Roles.Add(role); }
        role.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        Audit(actor, "ROLE", role.Id, id.HasValue ? "UPDATE" : "CREATE", new { Before = id.HasValue ? before : null, After = Snapshot(role) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(role.Id, ct);
    }

    public async Task<ServiceResult<RoleItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct)
    {
        if (request.Status is not ("ACTIVE" or "INACTIVE")) return Invalid<RoleItem>(new Dictionary<string, string[]> { ["status"] = ["Trạng thái không hợp lệ."] });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<RoleItem>();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return Missing<RoleItem>();
        if (IsAdministrator(role.Code) && request.Status != "ACTIVE") return Conflict<RoleItem>("Không ngừng áp dụng vai trò quản trị hệ thống.");
        if (role.Status != request.Status)
        {
            var before = Snapshot(role);
            role.Status = request.Status;
            await db.Users.Where(u => u.UserRoles.Any(ur => ur.RoleId == id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1), ct);
            Audit(actor, "ROLE", id, "STATUS", new { Before = before, After = Snapshot(role) });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<bool>();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return Missing<bool>();
        var item = await db.Roles.Where(r => r.Id == id).Select(RoleProjection).SingleAsync(ct);
        if (!item.CanDelete) return Conflict<bool>(item.DeleteBlockedReason);
        Audit(actor, "ROLE", id, "DELETE", Snapshot(role));
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<UserRolesDetail>> UserRolesAsync(ulong id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(UserProjection).SingleOrDefaultAsync(ct);
        if (user == null) return Missing<UserRolesDetail>();
        var roles = await db.Roles.AsNoTracking().Where(r => r.UserRoles.Any(ur => ur.UserId == id)).OrderBy(r => r.Name).Select(RoleProjection).ToArrayAsync(ct);
        return ServiceResult<UserRolesDetail>.Success(new(user, roles));
    }

    public async Task<ServiceResult<UserRolesDetail>> AssignUserRolesAsync(ulong id, AssignUserRolesRequest request, ulong actor, CancellationToken ct)
    {
        if (request.RoleIds == null || request.RoleIds.Length > 100 || request.RoleIds.Any(x => x == 0) || request.RoleIds.Distinct().Count() != request.RoleIds.Length)
            return Invalid<UserRolesDetail>(new Dictionary<string, string[]> { ["roleIds"] = ["Chọn tối đa 100 vai trò khác nhau."] });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<UserRolesDetail>();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<UserRolesDetail>();
        // Kept: compares the client value with user.security_version, not a removed column.
        if (request.Version == 0 || user.SecurityVersion != request.Version) return Conflict<UserRolesDetail>();
        var roles = await db.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToArrayAsync(ct);
        if (roles.Length != request.RoleIds.Length) return Conflict<UserRolesDetail>("Một hoặc nhiều vai trò không còn tồn tại.");
        var added = roles.Where(r => !user.UserRoles.Any(ur => ur.RoleId == r.Id)).ToArray();
        if (added.Any(r => !CanAssign(r, user))) return Conflict<UserRolesDetail>("Vai trò hoặc tài khoản ngừng hoạt động.");
        var removed = user.UserRoles.Where(ur => !request.RoleIds.Contains(ur.RoleId)).ToArray();
        if (id == actor && removed.Any(ur => IsAdministrator(ur.Role.Code))) return Conflict<UserRolesDetail>("Không tự thu hồi vai trò quản trị đang dùng.");
        foreach (var assignment in removed)
        {
            db.UserRoles.Remove(assignment);
            Audit(actor, "ROLE", assignment.RoleId, "REVOKE", new { UserId = id, Role = Snapshot(assignment.Role) });
        }
        foreach (var role in added) Grant(role, user, actor);
        if (added.Length + removed.Length > 0) user.SecurityVersion++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await UserRolesAsync(id, ct);
    }

    public async Task<ServiceResult<RoleItem>> AddUsersAsync(ulong id, AssignRoleUsersRequest request, ulong actor, CancellationToken ct)
    {
        if (request.UserIds == null || request.UserIds.Length is < 1 or > 100 || request.UserIds.Any(x => x == 0) || request.UserIds.Distinct().Count() != request.UserIds.Length)
            return Invalid<RoleItem>(new Dictionary<string, string[]> { ["userIds"] = ["Chọn 1–100 người dùng khác nhau."] });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<RoleItem>();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return Missing<RoleItem>();
        var users = await db.Users.Include(u => u.UserRoles).Where(u => request.UserIds.Contains(u.Id)).ToArrayAsync(ct);
        if (users.Length != request.UserIds.Length || users.Any(u => !CanAssign(role, u))) return Conflict<RoleItem>("Người dùng/vai trò không hoạt động.");
        if (users.Any(u => !u.UserRoles.Any(ur => ur.RoleId == id) && u.UserRoles.Count >= 100)) return Conflict<RoleItem>("Mỗi người dùng được gán tối đa 100 vai trò.");
        foreach (var user in users.Where(u => !u.UserRoles.Any(ur => ur.RoleId == id)))
        {
            Grant(role, user, actor);
            user.SecurityVersion++;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<RoleItem>> RemoveUserAsync(ulong id, ulong userId, uint version, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<RoleItem>();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return Missing<RoleItem>();
        if (userId == actor && IsAdministrator(role.Code)) return Conflict<RoleItem>("Không tự thu hồi vai trò quản trị đang dùng.");
        var assignment = await db.UserRoles.Include(ur => ur.User).SingleOrDefaultAsync(ur => ur.RoleId == id && ur.UserId == userId, ct);
        if (assignment == null) return Missing<RoleItem>();
        assignment.User.SecurityVersion++;
        db.UserRoles.Remove(assignment);
        Audit(actor, "ROLE", id, "REVOKE", new { UserId = userId, Role = Snapshot(role) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<DirectoryPage<IdentityScopeItem>>> ScopesAsync(string kind, IdentityListQuery query, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Query(query);
        if (kind is not ("school" or "branch")) errors["kind"] = ["Loại phạm vi không hợp lệ."];
        if (kind == "branch" && !query.SchoolId.HasValue) errors["schoolId"] = ["Chọn trường trước."];
        if (errors.Count > 0) return Invalid<DirectoryPage<IdentityScopeItem>>(errors);
        var rows = kind == "school"
            ? db.Schools.AsNoTracking().Where(s => s.Status == "ACTIVE").Select(s => new { s.Id, s.Code, s.Name, s.Status })
            : db.SchoolBranches.AsNoTracking().Where(b => b.SchoolId == query.SchoolId && b.Status == "ACTIVE" && b.School.Status == "ACTIVE").Select(b => new { b.Id, b.Code, b.Name, b.Status });
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(s => s.Name.Contains(search) || s.Code.Contains(search));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(s => s.Name).ThenBy(s => s.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(s => new IdentityScopeItem(s.Id, s.Code, s.Name, s.Status)).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<IdentityScopeItem>>.Success(new(items, query.Page, query.PageSize, count));
    }

    private void Grant(Role role, User user, ulong actor)
    {
        db.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
        Audit(actor, "ROLE", role.Id, "GRANT", new { UserId = user.Id, Role = Snapshot(role) });
    }
    private static object Snapshot(Role role) => new { role.Code, role.Name, role.Status };
    // shortcut: ERD v2 has no audit table (identity_audits), so audit entries are dropped until one is added.
    private static void Audit(ulong actor, string type, ulong id, string action, object data) { }
}
