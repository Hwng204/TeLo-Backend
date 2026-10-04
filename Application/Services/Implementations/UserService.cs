using System.Data;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Application.Common.IdentityManagementRules;

namespace Application.Services.Implement;

/// <summary>
/// Admin CRUD cho tài khoản người dùng. Dùng chung quy tắc với RoleService:
/// mọi thao tác ghi chạy trong transaction Serializable, kiểm tra lại quyền admin của actor,
/// kiểm tra security version để chống ghi đè và ghi identity_audits.
/// </summary>
public sealed class UserService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher) : IUserService
{
    private static Dictionary<string, string[]> InactiveBranchError() =>
        new() { ["schoolBranchId"] = ["Phân hiệu không tồn tại hoặc đã ngừng hoạt động."] };

    public async Task<ServiceResult<DirectoryPage<IdentityUserItem>>> ListAsync(IdentityListQuery query, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Query(query, allowLockedAccount: true);
        if (errors.Count > 0) return Invalid<DirectoryPage<IdentityUserItem>>(errors);
        var rows = db.Users.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(u => u.Username.Contains(search) || u.FullName.Contains(search) || u.Email.Contains(search));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(u => u.Status == query.Status);
        if (query.SchoolId.HasValue) rows = rows.Where(u => u.SchoolBranch != null && u.SchoolBranch.SchoolId == query.SchoolId);
        if (query.SchoolBranchId.HasValue) rows = rows.Where(u => u.SchoolBranchId == query.SchoolBranchId);
        if (query.RoleId.HasValue) rows = rows.Where(u => u.UserRoles.Any(ur => ur.RoleId == query.RoleId));
        if (query.EligibleForRoleId.HasValue)
        {
            var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == query.EligibleForRoleId, ct);
            if (role == null) return Missing<DirectoryPage<IdentityUserItem>>();
            rows = rows.Where(u => role.Status == "ACTIVE" && u.Status == "ACTIVE" && !u.UserRoles.Any(ur => ur.RoleId == role.Id));
            if (role.SchoolId.HasValue) rows = rows.Where(u => u.SchoolBranch != null && u.SchoolBranch.SchoolId == role.SchoolId && u.SchoolBranch.Status == "ACTIVE" && u.SchoolBranch.School.Status == "ACTIVE");
            if (role.SchoolBranchId.HasValue) rows = rows.Where(u => u.SchoolBranchId == role.SchoolBranchId);
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(u => u.Username).ThenBy(u => u.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(IdentityProjections.User).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<IdentityUserItem>>.Success(new(items, query.Page, query.PageSize, count));
    }

    public async Task<ServiceResult<UserDetailItem>> GetAsync(ulong id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new
        {
            u.Id, u.Username, u.FullName, u.Email, u.MoetIdentifier, u.Status,
            SchoolId = u.SchoolBranch == null ? (ulong?)null : u.SchoolBranch.SchoolId,
            SchoolName = u.SchoolBranch == null ? null : u.SchoolBranch.School.Name,
            u.SchoolBranchId,
            SchoolBranchName = u.SchoolBranch == null ? null : u.SchoolBranch.Name,
            u.CreatedAt, IsTeacher = u.Teachers.Any(), IsStudent = u.Students.Any(), u.SecurityVersion
        }).SingleOrDefaultAsync(ct);
        if (user == null) return Missing<UserDetailItem>();
        var roles = await db.Roles.AsNoTracking().Where(r => r.UserRoles.Any(ur => ur.UserId == id))
            .OrderBy(r => r.Name).Select(IdentityProjections.Role).ToArrayAsync(ct);
        return ServiceResult<UserDetailItem>.Success(new(user.Id, user.Username, user.FullName, user.Email, user.MoetIdentifier,
            user.Status, user.SchoolId, user.SchoolName, user.SchoolBranchId, user.SchoolBranchName,
            user.CreatedAt, user.IsTeacher, user.IsStudent, user.SecurityVersion, roles));
    }

    public async Task<ServiceResult<UserDetailItem>> CreateAsync(CreateUserRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.CreateUser(request);
        if (errors.Count > 0) return Invalid<UserDetailItem>(errors);
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var moet = NullIfEmpty(request.MoetIdentifier);
        var roleIds = request.RoleIds ?? [];

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<UserDetailItem>();
        var (branchOk, branch) = await ActiveBranchAsync(request.SchoolBranchId, ct);
        if (!branchOk) return Invalid<UserDetailItem>(InactiveBranchError());
        var duplicates = await DuplicatesAsync(null, username, email, moet, ct);
        if (duplicates.Count > 0) return Duplicate<UserDetailItem>(duplicates);

        // Gán cả FK lẫn navigation: CanAssign/FitsScope đọc cả hai trước khi SaveChanges.
        var user = new User
        {
            Username = username, Email = email, FullName = request.FullName.Trim(), MoetIdentifier = moet,
            SchoolBranchId = branch?.Id, SchoolBranch = branch,
            Status = IdentityManagementValidator.NormalizeStatus(request.Status), CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToArrayAsync(ct);
        if (roles.Length != roleIds.Length) return Conflict<UserDetailItem>("Một hoặc nhiều vai trò không còn tồn tại.");
        if (roles.Any(r => !CanAssign(r, user)))
            return Conflict<UserDetailItem>("Vai trò hoặc tài khoản ngừng hoạt động, hoặc phạm vi trường/phân hiệu không phù hợp.");

        db.Users.Add(user);
        await db.SaveChangesAsync(ct); // cần user.Id cho user_roles và audit
        foreach (var role in roles)
        {
            db.UserRoles.Add(new UserRole { RoleId = role.Id, UserId = user.Id });
            role.UsedAt ??= DateTime.UtcNow;
            role.Version++;
            Audit(actor, "ROLE", role.Id, "GRANT", new { UserId = user.Id, role.Code });
        }
        Audit(actor, "USER", user.Id, "CREATE", Snapshot(user));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<ServiceResult<UserDetailItem>> UpdateAsync(ulong id, UpdateUserRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.UpdateUser(request);
        if (errors.Count > 0) return Invalid<UserDetailItem>(errors);
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var moet = NullIfEmpty(request.MoetIdentifier);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<UserDetailItem>();
        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<UserDetailItem>();
        if (request.Version == 0 || user.SecurityVersion != request.Version) return Conflict<UserDetailItem>();
        var duplicates = await DuplicatesAsync(id, username, email, moet, ct);
        if (duplicates.Count > 0) return Duplicate<UserDetailItem>(duplicates);

        var before = Snapshot(user);
        var branchChanged = user.SchoolBranchId != request.SchoolBranchId;
        if (branchChanged)
        {
            // Hồ sơ giáo viên/học sinh gắn với đơn vị; chuyển đơn vị phải đi qua module tương ứng.
            if (await HasProfileAsync(id, ct))
                return Conflict<UserDetailItem>("Tài khoản gắn hồ sơ giáo viên/học sinh. Hãy chuyển phân hiệu tại module tương ứng.");
            var (branchOk, branch) = await ActiveBranchAsync(request.SchoolBranchId, ct);
            if (!branchOk) return Invalid<UserDetailItem>(InactiveBranchError());
            user.SchoolBranchId = branch?.Id;
            user.SchoolBranch = branch;
            if (user.UserRoles.Any(ur => !FitsScope(ur.Role, user)))
                return Conflict<UserDetailItem>("Phân hiệu mới không phù hợp với phạm vi vai trò đang gán. Hãy thu hồi vai trò không phù hợp trước.");
        }

        // Chỉ tăng security_version khi thay đổi ảnh hưởng tới đăng nhập/phân quyền
        // để không buộc người dùng đăng xuất chỉ vì sửa họ tên.
        if (branchChanged || user.Username != username || user.Email != email) user.SecurityVersion++;
        user.Username = username;
        user.Email = email;
        user.FullName = request.FullName.Trim();
        user.MoetIdentifier = moet;
        Audit(actor, "USER", id, "UPDATE", new { Before = before, After = Snapshot(user) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<UserDetailItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.UserStatus(request);
        if (errors.Count > 0) return Invalid<UserDetailItem>(errors);
        var status = IdentityManagementValidator.NormalizeStatus(request.Status);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<UserDetailItem>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<UserDetailItem>();
        if (request.Version == 0 || user.SecurityVersion != request.Version) return Conflict<UserDetailItem>();
        if (id == actor && status != "ACTIVE") return Conflict<UserDetailItem>("Không thể tự khóa/ngừng hoạt động tài khoản đang dùng.");
        if (status == "ACTIVE" && await db.Teachers.AnyAsync(t => t.UserId == id && (t.EmploymentStatus == "RESIGNED" || t.EmploymentStatus == "INACTIVE"), ct))
            return Conflict<UserDetailItem>("Giáo viên đã nghỉ/ngừng công tác. Cần cập nhật trạng thái công tác trước khi mở tài khoản.");
        if (user.Status != status)
        {
            Audit(actor, "USER", id, "STATUS", new { Before = user.Status, After = status });
            user.Status = status;
            user.SecurityVersion++;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<UserDetailItem>> ResetPasswordAsync(ulong id, ResetUserPasswordRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.ResetPassword(request);
        if (errors.Count > 0) return Invalid<UserDetailItem>(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<UserDetailItem>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<UserDetailItem>();
        if (request.Version == 0 || user.SecurityVersion != request.Version) return Conflict<UserDetailItem>();
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityVersion++; // thu hồi mọi phiên đăng nhập cũ
        Audit(actor, "USER", id, "RESET_PASSWORD", new { user.Username });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<bool>();
        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Missing<bool>();
        if (version == 0 || user.SecurityVersion != version) return Conflict<bool>();
        if (id == actor) return Conflict<bool>("Không thể tự xóa tài khoản đang dùng.");
        if (user.UserRoles.Any(ur => IsAdministrator(ur.Role.Code)))
            return Conflict<bool>("Không xóa tài khoản quản trị hệ thống. Hãy thu hồi vai trò hoặc ngừng hoạt động.");
        if (await HasProfileAsync(id, ct))
            return Conflict<bool>("Tài khoản gắn hồ sơ giáo viên/học sinh không thể xóa. Hãy ngừng hoạt động để giữ lịch sử.");
        foreach (var assignment in user.UserRoles)
        {
            assignment.Role.UsedAt ??= DateTime.UtcNow;
            assignment.Role.Version++;
            Audit(actor, "ROLE", assignment.RoleId, "REVOKE", new { UserId = id, assignment.Role.Code });
        }
        db.UserRoles.RemoveRange(user.UserRoles.ToArray());
        Audit(actor, "USER", id, "DELETE", Snapshot(user));
        db.Users.Remove(user);
        // Nếu tài khoản đã phát sinh dữ liệu ở bảng khác (FK), MySQL trả lỗi 1451 và
        // IdentityControllerBase chuyển thành 409 CONFLICT; transaction tự rollback.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    // (true, null) = không chọn phân hiệu; (false, _) = phân hiệu không tồn tại/ngừng hoạt động.
    private async Task<(bool Ok, SchoolBranch? Branch)> ActiveBranchAsync(ulong? branchId, CancellationToken ct)
    {
        if (!branchId.HasValue) return (true, null);
        var branch = await db.SchoolBranches.Include(b => b.School).SingleOrDefaultAsync(b => b.Id == branchId, ct);
        return branch is { Status: "ACTIVE", School.Status: "ACTIVE" } ? (true, branch) : (false, null);
    }

    private Task<bool> HasProfileAsync(ulong id, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == id && (u.Teachers.Any() || u.Students.Any()), ct);

    private async Task<Dictionary<string, string[]>> DuplicatesAsync(ulong? id, string username, string email, string? moet, CancellationToken ct)
    {
        var others = db.Users.Where(u => !id.HasValue || u.Id != id.Value);
        var matches = await others
            .Where(u => u.Username == username || u.Email == email || (moet != null && u.MoetIdentifier == moet))
            .Select(u => new { u.Username, u.Email, u.MoetIdentifier })
            .ToArrayAsync(ct);

        var errors = new Dictionary<string, string[]>();
        if (matches.Any(u => u.Username == username)) errors["username"] = ["Tên đăng nhập đã tồn tại."];
        if (matches.Any(u => u.Email == email)) errors["email"] = ["Email đã được sử dụng."];
        if (moet != null && matches.Any(u => u.MoetIdentifier == moet)) errors["moetIdentifier"] = ["Mã định danh Bộ GD đã tồn tại."];
        return errors;
    }

    private static ServiceResult<T> Duplicate<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure("CONFLICT", "Dữ liệu bị trùng với tài khoản khác.", errors);
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object Snapshot(User u) => new { u.Username, u.Email, u.FullName, u.MoetIdentifier, u.SchoolBranchId, u.Status, u.SecurityVersion };
    private void Audit(ulong actor, string type, ulong id, string action, object data) => db.IdentityAudits.Add(new IdentityAudit
    { ActorUserId = actor, EntityType = type, EntityId = id, Action = action, Data = JsonSerializer.Serialize(data) });
}
