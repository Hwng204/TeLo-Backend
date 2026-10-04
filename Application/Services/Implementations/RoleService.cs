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
using Microsoft.Extensions.Configuration;
using static Application.Common.IdentityManagementRules;

namespace Application.Services.Implement;

public sealed class RoleService(ApplicationDbContext db, IMatrixRoleCatalog roleCatalog, IConfiguration configuration) : IRoleService
{
    private static readonly Expression<Func<Role, RoleItem>> RoleProjection = IdentityProjections.Role;
    private static readonly Expression<Func<User, IdentityUserItem>> UserProjection = IdentityProjections.User;

    public async Task<ServiceResult<DirectoryPage<RoleItem>>> ListAsync(IdentityListQuery query, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<RoleItem>>(errors);
        var rows = db.Roles.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(r => r.Code.Contains(search) || r.Name.Contains(search));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(r => r.Status == query.Status);
        if (query.SchoolId.HasValue) rows = rows.Where(r => r.SchoolId == query.SchoolId);
        if (query.SchoolBranchId.HasValue) rows = rows.Where(r => r.SchoolBranchId == query.SchoolBranchId);
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
        if (id.HasValue && (request.Version == 0 || role.Version != request.Version)) return Conflict<RoleItem>();
        if (id.HasValue && !string.Equals(role.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            return Invalid<RoleItem>(new Dictionary<string, string[]> { ["code"] = ["Không được thay đổi mã vai trò."] });
        if (!id.HasValue && IsAdministrator(request.Code.Trim())) return Conflict<RoleItem>("Không được tạo thêm mã quản trị hệ thống.");
        var requiresGlobalScope = roleCatalog.IsPrincipal(request.Code.Trim()) ||
            configuration.GetSection("SchoolDirectoryAuth:AdminRoleCodes").GetChildren()
                .Any(code => string.Equals(code.Value?.Trim(), request.Code.Trim(), StringComparison.OrdinalIgnoreCase));
        // Existing principal/directory-admin consumers authorize globally. A narrower
        // label here would misrepresent the permissions actually granted by those APIs.
        if (requiresGlobalScope && (request.SchoolId.HasValue || request.SchoolBranchId.HasValue))
            return Invalid<RoleItem>(new Dictionary<string, string[]> { ["schoolId"] = ["Mã vai trò này chỉ hỗ trợ phạm vi toàn hệ thống."] });
        if (id.HasValue && (role.IsSystem || IsAdministrator(role.Code)) && (role.SchoolId != request.SchoolId || role.SchoolBranchId != request.SchoolBranchId))
            return Conflict<RoleItem>("Không thay đổi phạm vi của vai trò hệ thống.");
        var scopeChanged = role.SchoolId != request.SchoolId || role.SchoolBranchId != request.SchoolBranchId;
        if ((!id.HasValue || scopeChanged) && request.SchoolId.HasValue && !await db.Schools.AnyAsync(s => s.Id == request.SchoolId && s.Status == "ACTIVE", ct))
            errors["schoolId"] = ["Trường không tồn tại hoặc đã ngừng hoạt động."];
        if ((!id.HasValue || scopeChanged) && request.SchoolBranchId.HasValue && !await db.SchoolBranches.AnyAsync(b => b.Id == request.SchoolBranchId && b.SchoolId == request.SchoolId && b.Status == "ACTIVE", ct))
            errors["schoolBranchId"] = ["Phân hiệu phải đang hoạt động và thuộc trường đã chọn."];
        if (errors.Count > 0) return Invalid<RoleItem>(errors);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.Code == code && (!id.HasValue || r.Id != id.Value), ct)) return Conflict<RoleItem>("Mã vai trò đã tồn tại.");
        var before = Snapshot(role);
        role.SchoolId = request.SchoolId;
        role.SchoolBranchId = request.SchoolBranchId;
        if (id.HasValue && scopeChanged)
        {
            var users = db.Users.Where(u => u.UserRoles.Any(ur => ur.RoleId == role.Id));
            if (await users.AnyAsync(u =>
                (request.SchoolId.HasValue && (u.SchoolBranch == null || u.SchoolBranch.SchoolId != request.SchoolId)) ||
                (request.SchoolBranchId.HasValue && u.SchoolBranchId != request.SchoolBranchId), ct))
                return Conflict<RoleItem>("Phạm vi mới không phù hợp với người dùng đang được gán. Hãy thu hồi các gán không phù hợp trước.");
            await users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1), ct);
        }
        if (!id.HasValue) { role.Code = code; role.IsSystem = requiresGlobalScope; db.Roles.Add(role); }
        else role.Version++;
        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
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
        if (request.Version == 0 || role.Version != request.Version) return Conflict<RoleItem>();
        if (IsAdministrator(role.Code) && request.Status != "ACTIVE") return Conflict<RoleItem>("Không ngừng áp dụng vai trò quản trị hệ thống.");
        if (role.Status != request.Status)
        {
            var before = Snapshot(role);
            role.Status = request.Status;
            role.Version++;
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
        if (version == 0 || role.Version != version) return Conflict<bool>();
        if (role.IsSystem || IsAdministrator(role.Code) || role.UsedAt.HasValue ||
            await db.Roles.AnyAsync(r => r.Id == id && (r.UserRoles.Any() || r.Permissions.Any()), ct))
            return Conflict<bool>("Vai trò hệ thống hoặc đã phát sinh sử dụng không thể xóa. Hãy ngừng áp dụng để giữ lịch sử.");
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
        var user = await UsersWithScope().Include(u => u.UserRoles).ThenInclude(ur => ur.Role).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<UserRolesDetail>();
        if (request.Version == 0 || user.SecurityVersion != request.Version) return Conflict<UserRolesDetail>();
        var roles = await db.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToArrayAsync(ct);
        if (roles.Length != request.RoleIds.Length) return Conflict<UserRolesDetail>("Một hoặc nhiều vai trò không còn tồn tại.");
        var added = roles.Where(r => !user.UserRoles.Any(ur => ur.RoleId == r.Id)).ToArray();
        if (added.Any(r => !CanAssign(r, user))) return Conflict<UserRolesDetail>("Vai trò hoặc tài khoản ngừng hoạt động, hoặc phạm vi trường/phân hiệu không phù hợp.");
        var removed = user.UserRoles.Where(ur => !request.RoleIds.Contains(ur.RoleId)).ToArray();
        if (id == actor && removed.Any(ur => IsAdministrator(ur.Role.Code))) return Conflict<UserRolesDetail>("Không tự thu hồi vai trò quản trị đang dùng.");
        foreach (var assignment in removed)
        {
            assignment.Role.UsedAt ??= DateTime.UtcNow;
            assignment.Role.Version++;
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
        if (request.Version == 0 || role.Version != request.Version) return Conflict<RoleItem>();
        var users = await UsersWithScope().Include(u => u.UserRoles).Where(u => request.UserIds.Contains(u.Id)).ToArrayAsync(ct);
        if (users.Length != request.UserIds.Length || users.Any(u => !CanAssign(role, u))) return Conflict<RoleItem>("Người dùng/vai trò không hoạt động hoặc không thuộc phạm vi được chọn.");
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
        if (version == 0 || role.Version != version) return Conflict<RoleItem>();
        if (userId == actor && IsAdministrator(role.Code)) return Conflict<RoleItem>("Không tự thu hồi vai trò quản trị đang dùng.");
        var assignment = await db.UserRoles.Include(ur => ur.User).SingleOrDefaultAsync(ur => ur.RoleId == id && ur.UserId == userId, ct);
        if (assignment == null) return Missing<RoleItem>();
        role.UsedAt ??= DateTime.UtcNow;
        role.Version++;
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

    private IQueryable<User> UsersWithScope() => db.Users.Include(u => u.SchoolBranch).ThenInclude(b => b!.School);
    private void Grant(Role role, User user, ulong actor)
    {
        db.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
        role.UsedAt ??= DateTime.UtcNow;
        role.Version++;
        Audit(actor, "ROLE", role.Id, "GRANT", new { UserId = user.Id, Role = Snapshot(role) });
    }
    private static object Snapshot(Role role) => new { role.Code, role.Name, role.Description, role.Status, role.SchoolId, role.SchoolBranchId, role.Version };
    private void Audit(ulong actor, string type, ulong id, string action, object data) => db.IdentityAudits.Add(new IdentityAudit
    { ActorUserId = actor, EntityType = type, EntityId = id, Action = action, Data = JsonSerializer.Serialize(data) });
}
