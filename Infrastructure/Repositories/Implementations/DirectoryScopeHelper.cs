using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

/// <summary>
/// ResolvedScope represents the resolved actor/school context for data filtering.
/// </summary>
internal sealed record ResolvedScope(ulong SchoolId, string? ProvinceCode, ulong[]? HomeroomClassIds = null);

/// <summary>
/// Helper chuyên trách phân giải phạm vi truy cập (Scope Resolver):
/// - Xác định Trường học (SchoolId) & Tỉnh/Thành (ProvinceCode) từ ActorUserId (người dùng đăng nhập).
/// - Nếu là Giáo viên và có cờ HomeroomOnly: xác định danh sách các lớp chủ nhiệm (HomeroomClassIds) để giới hạn dữ liệu.
/// Lưu ý: Nghiệp vụ quản lý Năm học đã nằm riêng trong IAcademicYearRepository.
/// </summary>
internal static class DirectoryScopeHelper
{
    public const string ActiveYear = "ACTIVE";

    public static async Task<(DirectoryReadStatus Status, ResolvedScope? Scope)> ResolveScopeAsync(
        ApplicationDbContext db,
        DirectoryScope requested,
        CancellationToken cancellationToken)
    {
        if (requested.SchoolId is { } schoolId)
        {
            var school = await db.Schools.AsNoTracking()
                .Where(s => s.Id == schoolId)
                .Select(s => new ResolvedScope(s.Id, s.ProvinceCode, null))
                .SingleOrDefaultAsync(cancellationToken);
            return school is null
                ? (DirectoryReadStatus.SchoolNotFound, null)
                : (DirectoryReadStatus.Success, school);
        }

        if (requested.ActorUserId is not { } actorUserId)
        {
            return (DirectoryReadStatus.ActorNotFound, null);
        }

        var actor = await db.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new
            {
                SchoolId = (ulong?)u.SchoolBranch!.SchoolId,
                ProvinceCode = u.SchoolBranch!.School.ProvinceCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return (DirectoryReadStatus.ActorNotFound, null);
        }

        if (actor.SchoolId is not { } actorSchoolId)
        {
            return (DirectoryReadStatus.SchoolScopeMissing, null);
        }

        ulong[]? homeroomClassIds = null;
        if (requested.HomeroomOnly)
        {
            homeroomClassIds = await db.Teachers.AsNoTracking()
                .Where(t => t.UserId == actorUserId && t.ClassId != null)
                .Select(t => t.ClassId!.Value)
                .ToArrayAsync(cancellationToken);
        }

        return (DirectoryReadStatus.Success,
            new ResolvedScope(actorSchoolId, actor.ProvinceCode, homeroomClassIds));
    }

    public static Task<ulong?> ResolveActiveYearIdAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken) =>
        db.AcademicYears.AsNoTracking()
            .Where(y => y.Status == ActiveYear)
            .OrderByDescending(y => y.StartDate)
            .Select(y => (ulong?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public static IQueryable<StudentEnrollment> CurrentEnrollments(ApplicationDbContext db, ulong schoolId) =>
        db.StudentEnrollments.AsNoTracking().Where(e =>
            e.SchoolClass.SchoolBranch.SchoolId == schoolId &&
            e.AcademicYear.Status == ActiveYear &&
            e.Status == StudentEnrollmentStatusCodes.Active);
}
