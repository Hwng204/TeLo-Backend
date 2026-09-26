using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class SchoolDirectoryAdminRepository(ApplicationDbContext db)
    : ISchoolDirectoryAdminRepository
{
    private const int DuplicateKeyErrorNumber = 1062;

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
        if (target is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
        }

        if (await db.Students.AnyAsync(s => s.ActiveCode == command.Code, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        var student = new Student
        {
            Code = command.Code,
            FullName = command.FullName,
            DateOfBirth = command.DateOfBirth,
            Gender = command.Gender,
            AdmissionDate = command.AdmissionDate,
            Status = command.Status,
            Enrollments =
            {
                new StudentEnrollment
                {
                    SchoolClassId = target.Id,
                    AcademicYearId = target.AcademicYearId,
                    Status = StudentEnrollmentStatusCodes.Active,
                    StartedOn = command.AdmissionDate
                }
            }
        };
        db.Students.Add(student);

        // Profile and first enrollment land in the same SaveChanges transaction.
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

        // Bringing a deleted student back claims its code again, so check that case too, not only
        // an edited code, instead of leaning on the unique index and catching its error.
        var reactivating = student.Status == StudentStatusCodes.Inactive &&
            command.Status != StudentStatusCodes.Inactive;
        if ((student.Code != command.Code || reactivating) &&
            await db.Students.AnyAsync(
                s => s.ActiveCode == command.Code && s.Id != student.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        student.Code = command.Code;
        student.FullName = command.FullName;
        student.DateOfBirth = command.DateOfBirth;
        student.Gender = command.Gender;
        student.AdmissionDate = command.AdmissionDate;
        student.Status = command.Status;

        if (command.SchoolClassId is { } schoolClassId)
        {
            var target = await FindClassInSchoolAsync(
                command.SchoolId, schoolClassId, cancellationToken);
            if (target is null)
            {
                return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
            }

            // Placing a student who has no live class in that year is fine. Moving one who does is
            // a class transfer, which must keep the old period, so it is refused here instead of
            // silently overwriting the enrollment row.
            var current = student.Enrollments.FirstOrDefault(e =>
                e.AcademicYearId == target.AcademicYearId &&
                e.Status == StudentEnrollmentStatusCodes.Active);
            if (current is null)
            {
                student.Enrollments.Add(new StudentEnrollment
                {
                    StudentId = student.Id,
                    SchoolClassId = target.Id,
                    AcademicYearId = target.AcademicYearId,
                    Status = StudentEnrollmentStatusCodes.Active,
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

        var current = student.Enrollments.FirstOrDefault(e =>
            e.AcademicYearId == target.AcademicYearId &&
            e.Status == StudentEnrollmentStatusCodes.Active);
        if (current is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotEnrolledInYear);
        }

        // Already in that class: nothing to do, so a retried request is harmless.
        if (current.SchoolClassId == target.Id)
        {
            return DirectoryWriteResult<ulong>.Ok(student.Id);
        }

        var effectiveOn = command.EffectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveOn < current.StartedOn)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.InvalidEffectiveDate);
        }

        // EF issues the UPDATE before the INSERT, so the old row has already left the ACTIVE
        // state (and its generated unique key is NULL) when the new one is written.
        current.Status = StudentEnrollmentStatusCodes.TransferredOut;
        current.EndedOn = effectiveOn;
        student.Enrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SchoolClassId = target.Id,
            AcademicYearId = target.AcademicYearId,
            Status = StudentEnrollmentStatusCodes.Active,
            StartedOn = effectiveOn
        });

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
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
        student.Status = StudentStatusCodes.Inactive;
        foreach (var enrollment in student.Enrollments
            .Where(e => e.Status == StudentEnrollmentStatusCodes.Active))
        {
            enrollment.Status = StudentEnrollmentStatusCodes.TransferredOut;
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

        var teacher = await ResolveHomeroomTeacherAsync(
            command.SchoolId, command.HomeroomTeacherId, null, cancellationToken);
        if (teacher.Status is not DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(teacher.Status);
        }

        if (await db.SchoolClasses.AnyAsync(
            c => c.SchoolBranchId == command.SchoolBranchId &&
                c.AcademicYearId == command.AcademicYearId &&
                c.Code == command.Code,
            cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassCode);
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var saved = await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
        if (saved.Status is not DirectoryWriteStatus.Success)
        {
            return saved;
        }

        if (teacher.Value is { } newTeacher)
        {
            newTeacher.ClassId = schoolClass.Id;
            var assigned = await SaveAsync(
                () => schoolClass.Id, DirectoryWriteStatus.TeacherAlreadyHomeroom, cancellationToken);
            if (assigned.Status is not DirectoryWriteStatus.Success)
            {
                return assigned;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return DirectoryWriteResult<ulong>.Ok(schoolClass.Id);
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

        // Enrollment rows point at (class, academic year); moving the class to another year would
        // strand them, so the year is fixed once anybody is enrolled.
        if (schoolClass.AcademicYearId != command.AcademicYearId &&
            await db.StudentEnrollments.AnyAsync(
                e => e.SchoolClassId == schoolClass.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.AcademicYearInvalid);
        }

        if ((schoolClass.Code != command.Code ||
                schoolClass.SchoolBranchId != command.SchoolBranchId ||
                schoolClass.AcademicYearId != command.AcademicYearId) &&
            await db.SchoolClasses.AnyAsync(
                c => c.SchoolBranchId == command.SchoolBranchId &&
                    c.AcademicYearId == command.AcademicYearId &&
                    c.Code == command.Code &&
                    c.Id != schoolClass.Id,
                cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateClassCode);
        }

        var teacher = await ResolveHomeroomTeacherAsync(
            command.SchoolId, command.HomeroomTeacherId, schoolClass.Id, cancellationToken);
        if (teacher.Status is not DirectoryWriteStatus.Success)
        {
            return DirectoryWriteResult<ulong>.Fail(teacher.Status);
        }

        schoolClass.SchoolBranchId = command.SchoolBranchId;
        schoolClass.Code = command.Code;
        schoolClass.Name = command.Name;
        schoolClass.AcademicYearId = command.AcademicYearId;
        schoolClass.GradeLevelId = command.GradeLevelId;
        schoolClass.Status = command.Status;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // teachers.class_id is unique, so the outgoing homeroom teacher is cleared in its own save.
        var current = await db.Teachers
            .FirstOrDefaultAsync(t => t.ClassId == schoolClass.Id, cancellationToken);
        if (current is not null && current.Id != command.HomeroomTeacherId)
        {
            current.ClassId = null;
            var cleared = await SaveAsync(
                () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
            if (cleared.Status is not DirectoryWriteStatus.Success)
            {
                return cleared;
            }
        }

        if (teacher.Value is { } newTeacher && newTeacher.ClassId != schoolClass.Id)
        {
            newTeacher.ClassId = schoolClass.Id;
        }

        var saved = await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
        if (saved.Status is not DirectoryWriteStatus.Success)
        {
            return saved;
        }

        await transaction.CommitAsync(cancellationToken);
        return DirectoryWriteResult<ulong>.Ok(schoolClass.Id);
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

        if (await db.StudentEnrollments.AnyAsync(
            e => e.SchoolClassId == classId &&
                e.Status == StudentEnrollmentStatusCodes.Active,
            cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassHasActiveStudents);
        }

        schoolClass.Status = SchoolClassStatusCodes.Inactive;
        return await SaveAsync(
            () => schoolClass.Id, DirectoryWriteStatus.DuplicateClassCode, cancellationToken);
    }

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
            .Include(s => s.Enrollments)
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
