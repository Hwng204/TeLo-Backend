using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class TeacherRepository(
    ApplicationDbContext db, IMatrixRoleCatalog roles, IConfiguration configuration) : ITeacherRepository
{
    private string[] AdminRoles => configuration.GetSection("SchoolDirectoryAuth:AdminRoleCodes")
        .GetChildren().Select(x => x.Value!).Where(x => !string.IsNullOrWhiteSpace(x)).DefaultIfEmpty("OperationalAdmin").ToArray();

    public async Task<TeacherResolvedScope> ResolveScopeAsync(TeacherScope scope, bool write, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking()
            .Include(x => x.SchoolBranch).ThenInclude(x => x!.School)
            .SingleOrDefaultAsync(x => x.Id == scope.ActorUserId && x.Status == "ACTIVE", ct)
            ?? throw Error("UNAUTHORIZED", "Tài khoản không còn hoạt động.");
        var codes = await db.UserRoles.AsNoTracking().WhereEffective()
            .Where(assignment => assignment.UserId == actor.Id)
            .Select(assignment => assignment.Role.Code).ToArrayAsync(ct);
        var admin = codes.Any(x => AdminRoles.Contains(x, StringComparer.OrdinalIgnoreCase));
        if (scope.SchoolId is { } schoolId)
        {
            if (!admin) throw Error("FORBIDDEN", "Chỉ admin vận hành được chọn trường để quản lý giáo viên.");
            var school = await db.Schools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == schoolId, ct)
                ?? throw Error("SCHOOL_NOT_FOUND", "Không tìm thấy trường.");
            var isUnitActive = school.Status == "ACTIVE";
            if (scope.BranchId is { } branchId)
            {
                var branch = await db.SchoolBranches.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == branchId && x.SchoolId == schoolId, ct)
                    ?? throw Error("BRANCH_NOT_FOUND", "Không tìm thấy phân hiệu trong trường đã chọn.");
                isUnitActive &= branch.Status == "ACTIVE";
            }
            else if (write) throw Error("VALIDATION_ERROR", "Phải chọn phân hiệu để quản lý giáo viên.");
            return new(schoolId, scope.BranchId, true, isUnitActive);
        }

        if (write || (!codes.Any(roles.IsPrincipal) && !codes.Any(roles.IsPht)))
            throw Error("FORBIDDEN", "Tài khoản không có quyền xem hồ sơ giáo viên.");
        var unit = actor.SchoolBranch;
        if (unit is null || unit.Status != "ACTIVE" || unit.School.Status != "ACTIVE")
            throw Error("FORBIDDEN", "Tài khoản chưa được gán trường/phân hiệu đang hoạt động.");
        return new(unit.SchoolId, codes.Any(roles.IsPrincipal) ? null : unit.Id, false, true);
    }

    private IQueryable<Teacher> Scoped(TeacherResolvedScope scope) => db.Teachers
        .Where(x => x.User.SchoolBranch != null && x.User.SchoolBranch.SchoolId == scope.SchoolId &&
            (!scope.BranchId.HasValue || x.User.SchoolBranchId == scope.BranchId));

    private static IQueryable<Teacher> IncludeProfile(IQueryable<Teacher> query) => query
        .Include(x => x.User).ThenInclude(x => x.SchoolBranch)
        .Include(x => x.User).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role);

    public async Task<(IReadOnlyList<TeacherListRow> Items, int Total)> ListAsync(
        TeacherResolvedScope scope, TeacherFilter filter, CancellationToken ct)
    {
        await ValidateBranchFilterAsync(scope, filter.BranchId, ct);
        var query = Scoped(scope).AsNoTracking();
        if (filter.BranchId is { } branchId) query = query.Where(x => x.User.SchoolBranchId == branchId);
        if (filter.Search is { } search) query = query.Where(x =>
            x.User.FullName.Contains(search) || (x.User.MoetIdentifier != null && x.User.MoetIdentifier.Contains(search)));
        // Employment status is derived from the account status (see TeacherListRow below).
        if (filter.EmploymentStatus is { } employment) query = query.Where(x => (x.User.Status == "ACTIVE" ? "WORKING" : "INACTIVE") == employment);
        if (filter.AccountStatus is { } status) query = query.Where(x => x.User.Status == status);
        if (filter.Gender is { } gender) query = query.Where(x => x.Gender == gender);
        var total = await query.CountAsync(ct);
        var sorted = (filter.SortBy, filter.Descending) switch
        {
            ("staffCode", false) => query.OrderBy(x => x.User.MoetIdentifier),
            ("staffCode", true) => query.OrderByDescending(x => x.User.MoetIdentifier),
            ("createdAt", false) => query.OrderBy(x => x.User.CreatedAt),
            ("createdAt", true) => query.OrderByDescending(x => x.User.CreatedAt),
            (_, true) => query.OrderByDescending(x => x.User.FullName),
            _ => query.OrderBy(x => x.User.FullName)
        };
        // List queries never load password hashes or the full role collection.
        var items = await sorted.ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new TeacherListRow(
                x.Id, x.User.MoetIdentifier, x.User.FullName, x.Gender,
                x.User.Status == "ACTIVE" ? "WORKING" : "INACTIVE", x.User.Status,
                x.User.SchoolBranch!.SchoolId, x.User.SchoolBranchId!.Value,
                x.User.SchoolBranch.Name)).ToArrayAsync(ct);
        return (items, total);
    }

    public async Task<Teacher> GetAsync(TeacherResolvedScope scope, ulong id, bool tracking, CancellationToken ct)
    {
        var query = IncludeProfile(Scoped(scope));
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw Error("TEACHER_NOT_FOUND", "Không tìm thấy giáo viên trong phạm vi đã chọn.");
    }

    public async Task<TeacherReferenceRows> ReferencesAsync(TeacherResolvedScope scope, CancellationToken ct)
    {
        var subjects = await db.Subjects.AsNoTracking().Where(x => x.Status == "ACTIVE")
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new TeacherOptionRow(x.Id, "", x.Name, x.Status)).ToArrayAsync(ct);
        var branches = await db.SchoolBranches.AsNoTracking().Where(x => x.SchoolId == scope.SchoolId &&
                (!scope.BranchId.HasValue || x.Id == scope.BranchId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new TeacherOptionRow(x.Id, x.Code, x.Name, x.Status)).ToArrayAsync(ct);
        return new(subjects, branches);
    }

    public async Task<(IReadOnlyList<TeacherOptionRow> Items, int Total)> SchoolsAsync(
        ulong actorId, string? search, int page, int size, CancellationToken ct)
    {
        var allowed = AdminRoles;
        if (!await db.UserRoles.WhereEffective().AnyAsync(assignment => assignment.UserId == actorId &&
                assignment.User.Status == "ACTIVE" && allowed.Contains(assignment.Role.Code), ct))
            throw Error("FORBIDDEN", "Chỉ admin vận hành được chọn trường.");
        var query = db.Schools.AsNoTracking();
        if (search != null) query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new TeacherOptionRow(x.Id, x.Code, x.Name, x.Status)).ToArrayAsync(ct), total);
    }

    public async Task<(IReadOnlyList<TeacherOptionRow> Items, int Total)> BranchesAsync(
        ulong actorId, ulong schoolId, string? search, int page, int size, CancellationToken ct)
    {
        await ResolveScopeAsync(new(actorId, schoolId), false, ct);
        var query = db.SchoolBranches.AsNoTracking().Where(x => x.SchoolId == schoolId);
        if (search != null) query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        var total = await query.CountAsync(ct);
        return (await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new TeacherOptionRow(x.Id, x.Code, x.Name, x.Status)).ToArrayAsync(ct), total);
    }

    // Staff code is stored in the unique user.moet_identifier; teacher.email is not unique (no duplicate check).
    public async Task ValidateUniqueAsync(ulong? userId, string? staffCode, string username, CancellationToken ct)
    {
        if (staffCode != null && await db.Users.AnyAsync(x => x.Id != userId && x.MoetIdentifier == staffCode, ct))
            throw Error("STAFF_CODE_DUPLICATE", "Mã cán bộ đã tồn tại.");
        if (await db.Users.AnyAsync(x => x.Id != userId && x.Username == username, ct))
            throw Error("USERNAME_DUPLICATE", "Tên đăng nhập đã tồn tại.");
    }

    public async Task<Role> GetTeacherRoleAsync(CancellationToken ct)
    {
        var code = configuration["TeacherManagement:TeacherRoleCode"] ?? "TEACHER";
        if (IsPrivilegedRole(code))
            throw Error("TEACHER_ROLE_MISSING", "Vai trò giáo viên không được trùng vai trò quản trị hoặc ban giám hiệu.");
        return await db.Roles.SingleOrDefaultAsync(x => x.Code == code && x.Status == "ACTIVE", ct)
            ?? throw Error("TEACHER_ROLE_MISSING", "Chưa cấu hình vai trò giáo viên. Cần thiết lập TeacherManagement:TeacherRoleCode và danh mục roles.");
    }

    public void EnsureManageable(Teacher teacher)
    {
        var teacherCode = configuration["TeacherManagement:TeacherRoleCode"] ?? "TEACHER";
        if (teacher.User.UserRoles.Any(x => IsPrivilegedRole(x.Role.Code) || (
                !string.Equals(x.Role.Code, teacherCode, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Role.Code, "TEACHER", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Role.Code, "GIAO_VIEN", StringComparison.OrdinalIgnoreCase) &&
                !roles.IsTeamLead(x.Role.Code))))
            throw Error("PROTECTED_ACCOUNT", "Tài khoản có quyền quản trị khác; hãy quản lý tại chức năng quản lý người dùng.");
    }

    public void Add(Teacher teacher) => db.Teachers.Add(teacher);

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw Error("CONCURRENCY_CONFLICT", "Dữ liệu đã được thay đổi. Vui lòng tải lại trước khi cập nhật.");
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { Number: 1062 })
        {
            var message = ex.GetBaseException().Message;
            var code = message.Contains("uq_user_moet_identifier") ? "STAFF_CODE_DUPLICATE" :
                message.Contains("uq_user_username") ? "USERNAME_DUPLICATE" : "DATA_CONFLICT";
            throw Error(code, "Dữ liệu bị trùng với một bản ghi khác.");
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { Number: 1452 })
        {
            throw Error("DATA_CONFLICT", "Dữ liệu tham chiếu đã thay đổi. Vui lòng tải lại.");
        }
    }

    private async Task ValidateBranchFilterAsync(TeacherResolvedScope scope, ulong? id, CancellationToken ct)
    {
        if (id is null) return;
        if ((scope.BranchId is { } branch && branch != id) ||
            !await db.SchoolBranches.AnyAsync(x => x.Id == id && x.SchoolId == scope.SchoolId, ct))
            throw Error("FORBIDDEN", "Phân hiệu nằm ngoài phạm vi được phép.");
    }

    private static TeacherManagementException Error(string code, string message) => new(code, message);

    private bool IsPrivilegedRole(string code) =>
        AdminRoles.Contains(code, StringComparer.OrdinalIgnoreCase) || roles.IsPrincipal(code) || roles.IsPht(code);
}
