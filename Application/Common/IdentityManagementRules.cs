using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Application.Common;

public static class IdentityManagementRules
{
    // Codes the old migration flagged is_system; roles are global, so the flag is derived from the code.
    private static readonly string[] FixedSystemRoleCodes =
        ["ADMIN", "OPERATIONALADMIN", "TEACHER", "GIAO_VIEN", "STUDENT", "HOC_SINH", "HIEU_TRUONG", "PRINCIPAL", "PHT", "PHO_HIEU_TRUONG", "VICE_PRINCIPAL", "TEAM_LEAD", "TO_TRUONG", "SUBJECT_COORDINATOR"];

    public static string[] SystemRoleCodes(IMatrixRoleCatalog catalog) => FixedSystemRoleCodes
        .Concat(catalog.PhtRoleCodes).Concat(catalog.PrincipalRoleCodes).Concat(catalog.TeamLeadRoleCodes)
        .Select(code => code.Trim().ToUpperInvariant()).Distinct().ToArray();

    // Recheck inside the write transaction after locking the actor. Two admins
    // cannot revoke each other's last grant using already validated JWTs.
    public static async Task<bool> AuthorizeMutationAsync(ApplicationDbContext db, ulong actor, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM `user` WHERE user_id = {actor} FOR UPDATE").ToArrayAsync(ct);
        return await db.Users.AnyAsync(u => u.Id == actor && u.Status == "ACTIVE" && u.UserRoles.Any(ur =>
            ur.Role.Status == "ACTIVE" && (ur.Role.Code == "ADMIN" || ur.Role.Code == "OperationalAdmin")), ct);
    }

    public static ServiceResult<T> Forbidden<T>() => ServiceResult<T>.Failure("FORBIDDEN", "Bạn không còn quyền quản trị hệ thống. Vui lòng đăng nhập lại.");
    public static bool IsAdministrator(string code) =>
        code.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) || code.Equals("OperationalAdmin", StringComparison.OrdinalIgnoreCase);

    public static bool CanAssign(Role role, User user) => role.Status == "ACTIVE" && user.Status == "ACTIVE";

    public static ServiceResult<T> Invalid<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure("VALIDATION_ERROR", "Vui lòng kiểm tra dữ liệu nhập.", errors);
    public static ServiceResult<T> Missing<T>() => ServiceResult<T>.Failure("NOT_FOUND", "Không tìm thấy dữ liệu.");
    public static ServiceResult<T> Conflict<T>(string? message = null) =>
        ServiceResult<T>.Failure(message == null ? "STALE_VERSION" : "CONFLICT", message ?? "Dữ liệu đã thay đổi. Vui lòng tải lại trước khi tiếp tục.");
}
