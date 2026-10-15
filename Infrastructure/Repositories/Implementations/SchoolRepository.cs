using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class SchoolRepository(ApplicationDbContext db) : ISchoolRepository
{
    public async Task<DirectoryReadResult<DirectoryReferenceRows>> GetReferenceDataAsync(
        DirectoryScope requestedScope,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryReferenceRows>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var schoolClasses = db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId);
        if (scope.HomeroomClassIds is { } homeroomIds)
        {
            schoolClasses = schoolClasses.Where(c => homeroomIds.Contains(c.Id));
        }

        var forAdmin = requestedScope.SchoolId is not null;
        var yearId = academicYearId ?? await DirectoryScopeHelper.ResolveActiveYearIdAsync(db, cancellationToken);
        var enrolled = db.StudentEnrollments.AsNoTracking().Where(e =>
            e.SchoolClass.SchoolBranch.SchoolId == schoolId &&
            e.SchoolClass.AcademicYearId == yearId &&
            e.Status == StudentEnrollmentStatusCodes.Active);
        if (scope.HomeroomClassIds is { } leadIds)
        {
            enrolled = enrolled.Where(e => leadIds.Contains(e.SchoolClassId));
        }

        var branches = await db.SchoolBranches.AsNoTracking()
            .Where(b => b.SchoolId == schoolId)
            .OrderBy(b => b.Status == "ACTIVE" ? 0 : 1).ThenBy(b => b.Name).ThenBy(b => b.Id)
            .Select(b => new DirectoryBranchRow(
                b.Id, b.Code, b.Name, b.Status,
                schoolClasses.Count(c => c.SchoolBranchId == b.Id && c.AcademicYearId == yearId),
                enrolled.Count(e => e.SchoolClass.SchoolBranchId == b.Id)))
            .ToListAsync(cancellationToken);

        var grades = await db.GradeLevels.AsNoTracking()
            .Where(g => g.Status == "ACTIVE" && (forAdmin || schoolClasses.Any(c => c.GradeLevelId == g.Id)))
            .OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Select(g => new DirectoryOptionRow(g.Id, null, g.Name))
            .ToListAsync(cancellationToken);

        var years = await db.AcademicYears.AsNoTracking()
            .Where(y => forAdmin || schoolClasses.Any(c => c.AcademicYearId == y.Id))
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new DirectoryOptionRow(y.Id, y.Code, y.Name))
            .ToListAsync(cancellationToken);

        var classes = yearId is null
            ? new List<DirectoryOptionRow>()
            : await schoolClasses
                .Where(c => c.AcademicYearId == yearId && c.Status == SchoolClassStatusCodes.Active)
                .OrderBy(c => c.GradeLevel.Name).ThenBy(c => c.Name)
                .Select(c => new DirectoryOptionRow(c.Id, c.Code, c.Name))
                .ToListAsync(cancellationToken);

        return DirectoryReadResult<DirectoryReferenceRows>.Ok(
            new DirectoryReferenceRows(branches, grades, years, classes));
    }
}
