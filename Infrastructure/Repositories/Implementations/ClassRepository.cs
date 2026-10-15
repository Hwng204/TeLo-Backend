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
    private const string ClosedYear = "CLOSED";

    private sealed record ClassProjection(
        ulong Id,
        string Name,
        ulong GradeLevelId,
        string GradeLevelName,
        ulong AcademicYearId,
        string AcademicYearName,
        ulong SchoolBranchId,
        string SchoolBranchName,
        int StudentCount,
        string Status);

    public async Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, filter.Scope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(status);
        }

        var academicYearId = filter.AcademicYearId
            ?? await DirectoryScopeHelper.ResolveActiveYearIdAsync(db, cancellationToken);
        if (academicYearId is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(
                DirectoryReadStatus.ActiveAcademicYearNotFound);
        }

        var schoolId = scope.SchoolId;
        var yearId = academicYearId.Value;
        var classes = db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId && c.AcademicYearId == yearId);
        if (scope.HomeroomClassIds is { } homeroomIds)
        {
            classes = classes.Where(c => homeroomIds.Contains(c.Id));
        }

        if (filter.GradeLevelId is { } gradeId)
        {
            classes = classes.Where(c => c.GradeLevelId == gradeId);
        }

        if (filter.SchoolBranchId is { } branchId)
        {
            classes = classes.Where(c => c.SchoolBranchId == branchId);
        }

        if (filter.Status is not null)
        {
            classes = classes.Where(c => c.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            classes = classes.Where(c => c.Name.Contains(filter.Search));
        }

        var total = await classes.CountAsync(cancellationToken);
        var page = await Project(classes
                .OrderBy(c => c.GradeLevel.Name).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize))
            .ToListAsync(cancellationToken);

        var items = page.Select(ToRow).ToList();

        return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Ok(
            new DirectoryRowsPage<ClassDirectoryRow>(items, total));
    }

    public async Task<DirectoryReadResult<ClassDetailRow>> GetClassAsync(
        DirectoryScope requestedScope,
        ulong classId,
        int rosterPage,
        int rosterPageSize,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        if (scope.HomeroomClassIds is { } homeroomIds && !homeroomIds.Contains(classId))
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var projection = await Project(db.SchoolClasses.AsNoTracking()
                .Where(c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId))
            .SingleOrDefaultAsync(cancellationToken);
        if (projection is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var roster = db.StudentEnrollments.AsNoTracking().Where(e => e.SchoolClassId == classId);
        var total = await roster.CountAsync(cancellationToken);
        var students = await roster
            .OrderBy(e => e.Student.User.FullName).ThenBy(e => e.StudentId)
            .Skip((rosterPage - 1) * rosterPageSize).Take(rosterPageSize)
            .Select(e => new ClassStudentRow(
                e.StudentId, e.Student.User.Username, e.Student.User.FullName, e.Student.DateOfBirth,
                e.Student.Gender, e.SchoolClass.Name, e.Student.User.Status,
                e.EndedOn == null ? StudentEnrollmentStatusCodes.Active
                    : e.SchoolClass.AcademicYear.Status == ClosedYear ? StudentEnrollmentStatusCodes.Completed
                    : StudentEnrollmentStatusCodes.TransferredOut))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<ClassDetailRow>.Ok(new ClassDetailRow(
            ToRow(projection),
            new DirectoryRowsPage<ClassStudentRow>(students, total)));
    }

    private static IQueryable<ClassProjection> Project(IQueryable<SchoolClass> classes) =>
        classes.Select(c => new ClassProjection(
            c.Id, c.Name, c.GradeLevelId, c.GradeLevel.Name,
            c.AcademicYearId, c.AcademicYear.Name, c.SchoolBranchId, c.SchoolBranch.Name,
            c.Enrollments.Count(e => e.EndedOn == null),
            c.Status));

    // Class code = name and no homeroom teacher in DB v2; the API keeps both fields.

    private static ClassDirectoryRow ToRow(ClassProjection c) => new(
        c.Id, c.Name, c.Name, c.GradeLevelId, c.GradeLevelName,
        c.AcademicYearId, c.AcademicYearName, c.SchoolBranchId, c.SchoolBranchName,
        null, null, c.StudentCount, c.Status);

    public async Task<DirectoryWriteResult<ulong>> CreateClassAsync(
        CreateClassCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateClassReferencesAsync(
            command.SchoolId, command.SchoolBranchId, command.AcademicYearId,
            command.GradeLevelId, cancellationToken);
        if (validation is not DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(validation);
        }

        if (await NameTakenAsync(command.SchoolBranchId, command.AcademicYearId, command.Name, null, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassName);
        }

        var schoolClass = new SchoolClass
        {
            SchoolBranchId = command.SchoolBranchId,
            Name = command.Name,
            AcademicYearId = command.AcademicYearId,
            GradeLevelId = command.GradeLevelId,
            Status = command.Status
        };
        db.SchoolClasses.Add(schoolClass);

        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassName, cancellationToken);
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

        var validation = await ValidateClassReferencesAsync(
            command.SchoolId, command.SchoolBranchId, command.AcademicYearId,
            command.GradeLevelId, cancellationToken);
        if (validation is not DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(validation);
        }

        // Enrollments take their academic year from the class; moving the class to another year
        // would move the students' history with it, so the year is fixed once anybody is enrolled.
        if (schoolClass.AcademicYearId != command.AcademicYearId &&
            await db.StudentEnrollments.AnyAsync(
                e => e.SchoolClassId == schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.AcademicYearInvalid);
        }

        if (await NameTakenAsync(command.SchoolBranchId, command.AcademicYearId, command.Name, schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassName);
        }

        // Switching a class off through the edit form must obey the same rule as the delete button.
        if (command.Status == SchoolClassStatusCodes.Inactive &&
            schoolClass.Status != SchoolClassStatusCodes.Inactive &&
            await HasActiveStudentsAsync(schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassHasActiveStudents);
        }

        schoolClass.SchoolBranchId = command.SchoolBranchId;
        schoolClass.Name = command.Name;
        schoolClass.AcademicYearId = command.AcademicYearId;
        schoolClass.GradeLevelId = command.GradeLevelId;
        schoolClass.Status = command.Status;

        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassName, cancellationToken);
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

        if (await HasActiveStudentsAsync(classId, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassHasActiveStudents);
        }

        schoolClass.Status = SchoolClassStatusCodes.Inactive;
        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassName, cancellationToken);
    }

    // The live enrollment of one academic year; the service keeps it to at most one per student.

    private Task<bool> HasActiveStudentsAsync(ulong classId, CancellationToken cancellationToken) =>
        db.StudentEnrollments.AnyAsync(
            e => e.SchoolClassId == classId && e.EndedOn == null,
            cancellationToken);

    // uq_class_branch_id_academic_year_id_name: checked up front so the error names the field.

    private Task<bool> NameTakenAsync(
        ulong branchId,
        ulong yearId,
        string name,
        ulong? exceptClassId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses.AnyAsync(
            c => c.SchoolBranchId == branchId && c.AcademicYearId == yearId && c.Name == name &&
                c.Id != exceptClassId,
            cancellationToken);

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
            y => y.Id == academicYearId,
            cancellationToken))
        {
            return DirectoryWriteStatus.AcademicYearInvalid;
        }

        return await db.GradeLevels.AsNoTracking()
            .AnyAsync(g => g.Id == gradeLevelId, cancellationToken)
            ? DirectoryWriteStatus.Success
            : DirectoryWriteStatus.GradeLevelNotFound;
    }

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
            // Lost a race against a concurrent insert; report the conflict, not the driver error.
            return DirectoryWriteResult<ulong>.Fail(duplicateStatus);
        }
    }
}
