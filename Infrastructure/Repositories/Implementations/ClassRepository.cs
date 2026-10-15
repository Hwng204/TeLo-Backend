using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class ClassRepository(ApplicationDbContext db) : IClassRepository
{
    private const int DuplicateKeyErrorNumber = 1062;

    private sealed record ClassProjection(
        ulong Id,
        string Code,
        string Name,
        ulong GradeLevelId,
        string GradeLevelName,
        ulong AcademicYearId,
        string AcademicYearName,
        ulong SchoolBranchId,
        string SchoolBranchName,
        int StudentCount,
        string Status);

    // ─── READ OPERATIONS ────────────────────────────────────────────────────────

    public async Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, filter.Scope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var classes = db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId);

        if (scope.HomeroomClassIds is { } homeroomIds)
        {
            classes = classes.Where(c => homeroomIds.Contains(c.Id));
        }

        if (filter.GradeLevelId is { } gradeId)
        {
            classes = classes.Where(c => c.GradeLevelId == gradeId);
        }

        if (filter.AcademicYearId is { } yearId)
        {
            classes = classes.Where(c => c.AcademicYearId == yearId);
        }

        if (filter.SchoolBranchId is { } branchId)
        {
            classes = classes.Where(c => c.SchoolBranchId == branchId);
        }

        if (filter.Status is { } statusFilter)
        {
            classes = classes.Where(c => c.Status == statusFilter);
        }

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            classes = classes.Where(c => c.Code.Contains(search) || c.Name.Contains(search));
        }

        var total = await classes.CountAsync(cancellationToken);
        var page = await classes
            .OrderBy(c => c.GradeLevel.Name)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(c => new ClassProjection(
                c.Id,
                c.Code,
                c.Name,
                c.GradeLevelId,
                c.GradeLevel.Name,
                c.AcademicYearId,
                c.AcademicYear.Name,
                c.SchoolBranchId,
                c.SchoolBranch.Name,
                c.Enrollments.Count(e => e.Status == StudentEnrollmentStatusCodes.Active),
                c.Status))
            .ToListAsync(cancellationToken);

        var classIds = page.Select(c => c.Id).ToArray();
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.ClassId != null && classIds.Contains(t.ClassId.Value))
            .Select(t => new { ClassId = t.ClassId!.Value, t.Id, t.User.FullName })
            .ToListAsync(cancellationToken);
        var teacherByClass = teachers.ToDictionary(t => t.ClassId);

        var items = page.Select(c =>
        {
            teacherByClass.TryGetValue(c.Id, out var homeroom);
            return new ClassDirectoryRow(
                c.Id, c.Code, c.Name, c.GradeLevelId, c.GradeLevelName,
                c.AcademicYearId, c.AcademicYearName,
                c.SchoolBranchId, c.SchoolBranchName,
                homeroom?.Id, homeroom?.FullName,
                c.StudentCount, c.Status);
        }).ToList();

        return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Ok(
            new DirectoryRowsPage<ClassDirectoryRow>(items, total));
    }

    public async Task<DirectoryReadResult<ClassDetailRow>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        int rosterPage,
        int rosterPageSize,
        CancellationToken cancellationToken)
    {
        var (status, resolved) = await DirectoryScopeHelper.ResolveScopeAsync(db, scope, cancellationToken);
        if (resolved is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(status);
        }

        if (resolved.HomeroomClassIds is { } homeroomIds && !homeroomIds.Contains(classId))
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var schoolId = resolved.SchoolId;
        var found = await db.SchoolClasses.AsNoTracking()
            .Where(c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId)
            .Select(c => new ClassProjection(
                c.Id,
                c.Code,
                c.Name,
                c.GradeLevelId,
                c.GradeLevel.Name,
                c.AcademicYearId,
                c.AcademicYear.Name,
                c.SchoolBranchId,
                c.SchoolBranch.Name,
                c.Enrollments.Count(e => e.Status == StudentEnrollmentStatusCodes.Active),
                c.Status))
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var homeroom = await db.Teachers.AsNoTracking()
            .Where(t => t.ClassId == classId)
            .Select(t => new { t.Id, t.User.FullName })
            .SingleOrDefaultAsync(cancellationToken);

        var classRow = new ClassDirectoryRow(
            found.Id, found.Code, found.Name, found.GradeLevelId, found.GradeLevelName,
            found.AcademicYearId, found.AcademicYearName,
            found.SchoolBranchId, found.SchoolBranchName,
            homeroom?.Id, homeroom?.FullName,
            found.StudentCount, found.Status);

        var enrollments = db.StudentEnrollments.AsNoTracking()
            .Where(e => e.SchoolClassId == classId);

        var totalStudents = await enrollments.CountAsync(cancellationToken);
        var page = await enrollments
            .OrderBy(e => e.Status == StudentEnrollmentStatusCodes.Active ? 0 : 1)
            .ThenBy(e => e.Student.FullName)
            .ThenBy(e => e.StudentId)
            .Skip((rosterPage - 1) * rosterPageSize).Take(rosterPageSize)
            .Select(e => new ClassStudentRow(
                e.StudentId,
                e.Student.Code,
                e.Student.FullName,
                e.Student.DateOfBirth,
                e.Student.Gender,
                found.Name,
                e.Student.Status,
                e.Status))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<ClassDetailRow>.Ok(
            new ClassDetailRow(classRow, new DirectoryRowsPage<ClassStudentRow>(page, totalStudents)));
    }

    // ─── WRITE OPERATIONS ───────────────────────────────────────────────────────

    public async Task<DirectoryWriteResult<ulong>> CreateClassAsync(
        CreateClassCommand command,
        CancellationToken cancellationToken)
    {
        var refStatus = await ValidateClassReferencesAsync(
            command.SchoolId, command.SchoolBranchId, command.AcademicYearId, command.GradeLevelId,
            cancellationToken);
        if (refStatus != DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(refStatus);
        }

        if (await db.SchoolClasses.AnyAsync(
            c => c.SchoolBranchId == command.SchoolBranchId &&
                c.AcademicYearId == command.AcademicYearId &&
                c.Code == command.Code, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassCode);
        }

        if (await NameTakenAsync(command.SchoolBranchId, command.AcademicYearId, command.Name, null, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassName);
        }

        var (teacherStatus, homeroom) = await ResolveHomeroomTeacherAsync(
            command.SchoolId, command.HomeroomTeacherId, null, cancellationToken);
        if (teacherStatus != DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(teacherStatus);
        }

        var schoolClass = new SchoolClass
        {
            SchoolBranchId = command.SchoolBranchId,
            Code = command.Code,
            Name = command.Name,
            AcademicYearId = command.AcademicYearId,
            GradeLevelId = command.GradeLevelId,
            Status = command.Status
        };
        db.SchoolClasses.Add(schoolClass);

        if (homeroom is not null)
        {
            homeroom.SchoolClass = schoolClass;
        }

        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
    }

    public async Task<DirectoryWriteResult<ulong>> UpdateClassAsync(
        UpdateClassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolClass = await FindTrackedClassInSchoolAsync(
            command.SchoolId, command.ClassId, cancellationToken);
        if (schoolClass is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
        }

        var refStatus = await ValidateClassReferencesAsync(
            command.SchoolId, command.SchoolBranchId, command.AcademicYearId, command.GradeLevelId,
            cancellationToken);
        if (refStatus != DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(refStatus);
        }

        var code = string.IsNullOrEmpty(command.Code) ? schoolClass.Code : command.Code;
        if ((schoolClass.Code != code ||
                schoolClass.SchoolBranchId != command.SchoolBranchId ||
                schoolClass.AcademicYearId != command.AcademicYearId) &&
            await db.SchoolClasses.AnyAsync(
                c => c.SchoolBranchId == command.SchoolBranchId &&
                    c.AcademicYearId == command.AcademicYearId &&
                    c.Code == code &&
                    c.Id != schoolClass.Id,
                cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassCode);
        }

        if (await NameTakenAsync(command.SchoolBranchId, command.AcademicYearId, command.Name, schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassName);
        }

        if (command.Status == SchoolClassStatusCodes.Inactive &&
            schoolClass.Status != SchoolClassStatusCodes.Inactive &&
            await HasActiveStudentsAsync(schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassHasActiveStudents);
        }

        var (teacherStatus, homeroom) = await ResolveHomeroomTeacherAsync(
            command.SchoolId, command.HomeroomTeacherId, schoolClass.Id, cancellationToken);
        if (teacherStatus != DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(teacherStatus);
        }

        schoolClass.SchoolBranchId = command.SchoolBranchId;
        schoolClass.Code = code;
        schoolClass.Name = command.Name;
        schoolClass.AcademicYearId = command.AcademicYearId;
        schoolClass.GradeLevelId = command.GradeLevelId;
        schoolClass.Status = command.Status;

        var currentTeacher = await db.Teachers
            .SingleOrDefaultAsync(t => t.ClassId == schoolClass.Id, cancellationToken);
        if (currentTeacher is not null && currentTeacher.Id != command.HomeroomTeacherId)
        {
            currentTeacher.ClassId = null;
        }

        if (homeroom is not null)
        {
            homeroom.ClassId = schoolClass.Id;
        }

        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
    }

    public async Task<DirectoryWriteResult<ulong>> DeactivateClassAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken)
    {
        var schoolClass = await FindTrackedClassInSchoolAsync(schoolId, classId, cancellationToken);
        if (schoolClass is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
        }

        if (schoolClass.Status == SchoolClassStatusCodes.Inactive)
        {
            return DirectoryWriteResult<ulong>.Ok(schoolClass.Id);
        }

        if (await HasActiveStudentsAsync(classId, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassHasActiveStudents);
        }

        schoolClass.Status = SchoolClassStatusCodes.Inactive;
        var homeroom = await db.Teachers
            .SingleOrDefaultAsync(t => t.ClassId == classId, cancellationToken);
        if (homeroom is not null)
        {
            homeroom.ClassId = null;
        }

        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────────────────

    private Task<SchoolClass?> FindTrackedClassInSchoolAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses
            .SingleOrDefaultAsync(
                c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId, cancellationToken);

    private async Task<DirectoryWriteStatus> ValidateClassReferencesAsync(
        ulong schoolId,
        ulong schoolBranchId,
        ulong academicYearId,
        ulong gradeLevelId,
        CancellationToken cancellationToken)
    {
        if (!await db.Schools.AsNoTracking().AnyAsync(s => s.Id == schoolId, cancellationToken))
        {
            return DirectoryWriteStatus.SchoolNotFound;
        }

        if (!await db.SchoolBranches.AsNoTracking().AnyAsync(
            b => b.Id == schoolBranchId && b.SchoolId == schoolId, cancellationToken))
        {
            return DirectoryWriteStatus.BranchNotInSchool;
        }

        if (!await db.AcademicYears.AsNoTracking().AnyAsync(
            y => y.Id == academicYearId, cancellationToken))
        {
            return DirectoryWriteStatus.AcademicYearInvalid;
        }

        return await db.GradeLevels.AsNoTracking()
            .AnyAsync(g => g.Id == gradeLevelId, cancellationToken)
            ? DirectoryWriteStatus.Success
            : DirectoryWriteStatus.GradeLevelNotFound;
    }

    private async Task<(DirectoryWriteStatus Status, Teacher? Value)> ResolveHomeroomTeacherAsync(
        ulong schoolId,
        ulong? teacherId,
        ulong? classId,
        CancellationToken cancellationToken)
    {
        if (teacherId is not { } id)
        {
            return (DirectoryWriteStatus.Success, null);
        }

        var teacher = await db.Teachers
            .SingleOrDefaultAsync(
                t => t.Id == id && t.User.SchoolBranch!.SchoolId == schoolId, cancellationToken);
        if (teacher is null)
        {
            return (DirectoryWriteStatus.TeacherNotInSchool, null);
        }

        return teacher.ClassId is null || teacher.ClassId == classId
            ? (DirectoryWriteStatus.Success, teacher)
            : (DirectoryWriteStatus.TeacherAlreadyHomeroom, null);
    }

    private Task<bool> NameTakenAsync(
        ulong branchId,
        ulong academicYearId,
        string name,
        ulong? ignoreClassId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses.AnyAsync(
            c => c.SchoolBranchId == branchId &&
                c.AcademicYearId == academicYearId &&
                c.Name == name &&
                (ignoreClassId == null || c.Id != ignoreClassId),
            cancellationToken);

    private Task<bool> HasActiveStudentsAsync(ulong classId, CancellationToken cancellationToken) =>
        db.StudentEnrollments.AnyAsync(
            e => e.SchoolClassId == classId &&
                e.Status == StudentEnrollmentStatusCodes.Active,
            cancellationToken);

    private async Task<DirectoryWriteResult<ulong>> SaveAsync(
        Func<ulong> id,
        DirectoryWriteStatus duplicateStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return DirectoryWriteResult<ulong>.Ok(id());
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: DuplicateKeyErrorNumber })
        {
            return DirectoryWriteResult<ulong>.Fail(duplicateStatus);
        }
    }
}
