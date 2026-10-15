using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

internal sealed record ResolvedScope(ulong SchoolId, string? ProvinceCode, ulong[]? HomeroomClassIds = null);

internal static class DirectoryScopeHelper
{
    private const string ActiveYear = "ACTIVE";

    public static async Task<(DirectoryReadStatus Status, ResolvedScope? Scope)> ResolveScopeAsync(
        ApplicationDbContext db,
        DirectoryScope requested,
        CancellationToken cancellationToken)
    {
        // Admin path: the route names the school, so the actor needs no branch of its own.
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
            // No homeroom column in DB v2: a plain teacher sees the classes they are assigned to (R5).
            homeroomClassIds = await db.TeacherAssignments.AsNoTracking()
                .Where(a => a.Teacher.UserId == actorUserId)
                .Select(a => a.ClassId)
                .Distinct()
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
            .OrderByDescending(y => y.Id)
            .Select(y => (ulong?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // A student can hold several rows in one year once a class transfer keeps the old period, so
    // only the open row counts as "current". Without this a transferred student appears twice.

    public static IQueryable<StudentEnrollment> CurrentEnrollments(ApplicationDbContext db, ulong schoolId) =>
        db.StudentEnrollments.AsNoTracking().Where(e =>
            e.SchoolClass.SchoolBranch.SchoolId == schoolId &&
            e.SchoolClass.AcademicYear.Status == ActiveYear &&
            e.EndedOn == null);
}
