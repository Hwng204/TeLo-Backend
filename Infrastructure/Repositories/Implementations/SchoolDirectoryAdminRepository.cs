using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class SchoolDirectoryAdminRepository(
    ApplicationDbContext db,
    IPasswordHasher<User> passwordHasher) : ISchoolDirectoryAdminRepository
{
    private const int DuplicateKeyErrorNumber = 1062;

    // Every student owns a user row (student.user_id NOT NULL): username = student code, no role and
    // a random password nobody knows, so the account cannot sign in.
    internal static User NewStudentUser(
        IPasswordHasher<User> hasher,
        string code,
        string fullName,
        ulong branchId,
        string status)
    {
        var user = new User
        {
            Username = code,
            FullName = fullName,
            SchoolBranchId = branchId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, Guid.NewGuid().ToString());
        return user;
    }

    public async Task<DirectoryWriteResult<ulong>> CreateStudentAsync(
        CreateStudentCommand command,
        CancellationToken cancellationToken)
    {
        if (!await SchoolExistsAsync(command.SchoolId, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.SchoolNotFound);
        }

        var target = await FindClassInSchoolAsync(
            command.SchoolId, command.SchoolClassId, cancellationToken);
        if (target is null || target.Status != SchoolClassStatusCodes.Active)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
        }

        // The student code is the account username, so it must not match any user at all.
        if (await db.Users.AnyAsync(u => u.Username == command.Code, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        var student = new Student
        {
            User = NewStudentUser(
                passwordHasher, command.Code, command.FullName, target.SchoolBranchId, command.Status),
            DateOfBirth = command.DateOfBirth,
            Gender = command.Gender,
            Enrollments =
            {
                new StudentEnrollment
                {
                    SchoolClassId = target.Id,
                    StartedOn = DateOnly.FromDateTime(DateTime.UtcNow)
                }
            }
        };
        db.Students.Add(student);

        // User, profile and first enrollment land in the same SaveChanges transaction.
        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
    }

    public async Task<DirectoryWriteResult<ulong>> UpdateStudentAsync(
        UpdateStudentCommand command,
        CancellationToken cancellationToken)
    {
        var student = await FindStudentInSchoolAsync(
            command.SchoolId, command.StudentId, cancellationToken);
        if (student is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotFound);
        }

        if (student.User.Username != command.Code &&
            await db.Users.AnyAsync(
                u => u.Username == command.Code && u.Id != student.UserId, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        student.User.Username = command.Code;
        student.User.FullName = command.FullName;
        student.User.Status = command.Status;
        student.DateOfBirth = command.DateOfBirth;
        student.Gender = command.Gender;

        if (command.SchoolClassId is { } schoolClassId)
        {
            var target = await FindClassInSchoolAsync(
                command.SchoolId, schoolClassId, cancellationToken);
            if (target is null || target.Status != SchoolClassStatusCodes.Active)
            {
                return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
            }

            // Placing a student who has no live class in that year is fine. Moving one who does is
            // a class transfer, which must keep the old period, so it is refused here instead of
            // silently overwriting the enrollment row.
            var current = OpenEnrollmentInYear(student, target.AcademicYearId);
            if (current is null)
            {
                if (student.Enrollments.Any(e => e.SchoolClassId == target.Id))
                {
                    return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.AlreadyEnrolledInClass);
                }

                student.Enrollments.Add(new StudentEnrollment
                {
                    StudentId = student.Id,
                    SchoolClassId = target.Id,
                    StartedOn = DateOnly.FromDateTime(DateTime.UtcNow)
                });
            }
            else if (current.SchoolClassId != target.Id)
            {
                return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.UseClassTransfer);
            }
        }

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
    }

    public async Task<DirectoryWriteResult<ulong>> TransferStudentClassAsync(
        TransferStudentClassCommand command,
        CancellationToken cancellationToken)
    {
        var student = await FindStudentInSchoolAsync(
            command.SchoolId, command.StudentId, cancellationToken);
        if (student is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotFound);
        }

        var target = await FindClassInSchoolAsync(
            command.SchoolId, command.SchoolClassId, cancellationToken);
        if (target is null || target.Status != SchoolClassStatusCodes.Active)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
        }

        var current = OpenEnrollmentInYear(student, target.AcademicYearId);
        if (current is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotEnrolledInYear);
        }

        // Already in that class: nothing to do, so a retried request is harmless.
        if (current.SchoolClassId == target.Id)
        {
            return DirectoryWriteResult<ulong>.Ok(student.Id);
        }

        // (class_id, student_id) is the key: a class the student already left cannot be re-entered.
        if (student.Enrollments.Any(e => e.SchoolClassId == target.Id))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.AlreadyEnrolledInClass);
        }

        var effectiveOn = command.EffectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveOn < current.StartedOn)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.InvalidEffectiveDate);
        }

        current.EndedOn = effectiveOn;
        student.Enrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SchoolClassId = target.Id,
            StartedOn = effectiveOn
        });
        student.User.SchoolBranchId = target.SchoolBranchId;

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.AlreadyEnrolledInClass, cancellationToken);
    }

    public async Task<DirectoryWriteResult<ulong>> DeactivateStudentAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var student = await FindStudentInSchoolAsync(schoolId, studentId, cancellationToken);
        if (student is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotFound);
        }

        // Logical delete: history rows and exam registrations stay untouched.
        student.User.Status = StudentStatusCodes.Inactive;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var enrollment in student.Enrollments.Where(e => e.EndedOn == null))
        {
            // A transfer may be dated ahead; never close a period before it starts.
            enrollment.EndedOn = enrollment.StartedOn > today ? enrollment.StartedOn : today;
        }

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
    }

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
    private static StudentEnrollment? OpenEnrollmentInYear(Student student, ulong academicYearId) =>
        student.Enrollments.FirstOrDefault(e =>
            e.SchoolClass.AcademicYearId == academicYearId && e.EndedOn == null);

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

    private Task<bool> SchoolExistsAsync(ulong schoolId, CancellationToken cancellationToken) =>
        db.Schools.AsNoTracking().AnyAsync(s => s.Id == schoolId, cancellationToken);

    private Task<SchoolClass?> FindClassInSchoolAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses.AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId, cancellationToken);

    private Task<SchoolClass?> FindTrackedClassInSchoolAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses
            .SingleOrDefaultAsync(
                c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId, cancellationToken);

    private Task<Student?> FindStudentInSchoolAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken) =>
        db.Students
            .Include(s => s.User)
            .Include(s => s.Enrollments).ThenInclude(e => e.SchoolClass)
            .SingleOrDefaultAsync(
                s => s.Id == studentId &&
                    s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId),
                cancellationToken);

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
