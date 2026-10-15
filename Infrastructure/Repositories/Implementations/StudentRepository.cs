using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Repositories.Implement;

public sealed class StudentRepository(ApplicationDbContext db, IPasswordHasher<User> passwordHasher) : IStudentRepository
{
    private const int DuplicateKeyErrorNumber = 1062;
    private const string ClosedYear = "CLOSED";

    public async Task<DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>> ListStudentsAsync(
        StudentDirectoryFilter filter,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, filter.Scope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var students = db.Students.AsNoTracking()
            .Where(s => s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId));
        if (scope.HomeroomClassIds is { } homeroomIds)
        {
            students = students.Where(s => s.Enrollments.Any(e => homeroomIds.Contains(e.SchoolClassId)));
        }

        // Explicit years use the last class period in that year; otherwise use the open active-year period.
        if (filter.AcademicYearId is not null || filter.ClassId is not null || filter.GradeLevelId is not null || filter.SchoolBranchId is not null)
        {
            var current = ListingEnrollments(schoolId, filter.AcademicYearId);
            if (filter.ClassId is { } classId)
            {
                current = current.Where(e => e.SchoolClassId == classId);
            }

            if (filter.GradeLevelId is { } gradeId)
            {
                current = current.Where(e => e.SchoolClass.GradeLevelId == gradeId);
            }

            if (filter.SchoolBranchId is { } branchId)
            {
                current = current.Where(e => e.SchoolClass.SchoolBranchId == branchId);
            }

            students = students.Where(s => current.Any(e => e.StudentId == s.Id));
        }

        if (filter.Status is not null)
        {
            students = students.Where(s => s.User.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            students = students.Where(s =>
                s.User.Username.Contains(filter.Search) || s.User.FullName.Contains(filter.Search));
        }

        var total = await students.CountAsync(cancellationToken);
        var page = await students
            .OrderBy(s => s.User.FullName).ThenBy(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(s => new
            {
                s.Id, Code = s.User.Username, s.User.FullName, s.DateOfBirth, s.Gender, s.User.Status
            })
            .ToListAsync(cancellationToken);

        var ids = page.Select(s => s.Id).ToArray();
        var currentRows = await ListingEnrollments(schoolId, filter.AcademicYearId)
            .Where(e => ids.Contains(e.StudentId))
            .Select(e => new
            {
                e.StudentId,
                e.StartedOn,
                ClassId = e.SchoolClassId,
                ClassName = e.SchoolClass.Name,
                e.SchoolClass.GradeLevelId,
                GradeLevelName = e.SchoolClass.GradeLevel.Name,
                e.SchoolClass.SchoolBranchId,
                SchoolBranchName = e.SchoolClass.SchoolBranch.Name
            })
            .ToListAsync(cancellationToken);
        var currentByStudent = currentRows
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.StartedOn).First());

        var items = page.Select(s =>
        {
            currentByStudent.TryGetValue(s.Id, out var c);
            return new StudentDirectoryRow(
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender,
                c?.GradeLevelId, c?.GradeLevelName, c?.ClassId, c?.ClassName,
                c?.SchoolBranchId, c?.SchoolBranchName, s.Status);
        }).ToList();

        return DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Ok(
            new DirectoryRowsPage<StudentDirectoryRow>(items, total));
    }

    public async Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope requestedScope,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var homeroomIds = scope.HomeroomClassIds;
        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == studentId &&
                s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId) &&
                (homeroomIds == null || s.Enrollments.Any(e => homeroomIds.Contains(e.SchoolClassId))))
            .Select(s => new
            {
                s.Id, Code = s.User.Username, s.User.FullName, s.DateOfBirth, s.Gender, s.User.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        // Newest first; on a same-day transfer the open row comes before the closed one.
        var rows = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolClass.SchoolBranch.SchoolId == schoolId)
            .OrderByDescending(e => e.SchoolClass.AcademicYearId)
            .ThenByDescending(e => e.StartedOn).ThenBy(e => e.EndedOn != null)
            .Select(e => new StudentEnrollmentRow(
                e.SchoolClass.AcademicYearId,
                e.SchoolClass.AcademicYear.Name,
                e.SchoolClass.AcademicYear.Status,
                e.SchoolClass.GradeLevelId,
                e.SchoolClass.GradeLevel.Name,
                e.SchoolClassId,
                e.SchoolClass.Name,
                e.SchoolClass.SchoolBranchId,
                e.SchoolClass.SchoolBranch.Name,
                null,
                null,
                e.EndedOn == null ? StudentEnrollmentStatusCodes.Active
                    : e.SchoolClass.AcademicYear.Status == ClosedYear ? StudentEnrollmentStatusCodes.Completed
                    : StudentEnrollmentStatusCodes.TransferredOut))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<StudentDetailRow>.Ok(new StudentDetailRow(
            student.Id, student.Code, student.FullName, student.DateOfBirth, student.Gender,
            student.Status, rows));
    }

    public async Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope requestedScope,
        ulong studentId,
        ulong classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await DirectoryScopeHelper.ResolveScopeAsync(db, requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        // A teacher sees the scores (of any class in the history) only for students of their own class.
        if (scope.HomeroomClassIds is { } homeroomIds &&
            !await db.StudentEnrollments.AsNoTracking().AnyAsync(
                e => e.StudentId == studentId && homeroomIds.Contains(e.SchoolClassId), cancellationToken))
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(DirectoryReadStatus.NotFound);
        }

        // One lookup proves the student, the class and the enrollment linking them are all in scope.
        var enrolled = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolClassId == classId &&
                e.SchoolClass.SchoolBranch.SchoolId == schoolId)
            .Select(e => new
            {
                e.SchoolClass.AcademicYearId,
                e.SchoolClass.GradeLevelId,
                e.SchoolClass.SchoolBranchId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (enrolled is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(
                DirectoryReadStatus.NotFound);
        }

        // ERD v2: one test_result per student and exam; the exam carries branch, semester, subject and grade.
        var scores = db.TestResults.AsNoTracking()
            .Where(r =>
                r.StudentId == studentId &&
                r.TotalScore != null &&
                r.Exam.ResultPublishedAt != null &&
                r.Exam.GradeLevelId == enrolled.GradeLevelId &&
                r.Exam.BranchId == enrolled.SchoolBranchId &&
                r.Exam.Semester.AcademicYearId == enrolled.AcademicYearId);

        var total = await scores.CountAsync(cancellationToken);
        var items = await scores
            .OrderByDescending(r => r.Exam.StartDate)
            .ThenBy(r => r.Exam.Subject.Name)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new StudentScoreRow(
                r.Id,
                r.ExamId,
                r.Exam.Name,
                r.Exam.SemesterId,
                r.Exam.Semester.Name,
                r.Exam.SubjectId,
                r.Exam.Subject.Name,
                r.Exam.StartDate,
                r.TotalScore!.Value,
                r.Exam.ResultPublishedAt!.Value))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Ok(
            new DirectoryRowsPage<StudentScoreRow>(items, total));
    }

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
        var studentCode = command.Code?.Trim();
        if (string.IsNullOrEmpty(studentCode))
            studentCode = await SuggestStudentCodeAsync(command.SchoolId, target.Id, cancellationToken);
        command = command with { Code = studentCode };
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

    private static StudentEnrollment? OpenEnrollmentInYear(Student student, ulong academicYearId) =>
        student.Enrollments.FirstOrDefault(e =>
            e.SchoolClass.AcademicYearId == academicYearId && e.EndedOn == null);

    private Task<bool> SchoolExistsAsync(ulong schoolId, CancellationToken cancellationToken) =>
        db.Schools.AsNoTracking().AnyAsync(s => s.Id == schoolId, cancellationToken);

    private Task<SchoolClass?> FindClassInSchoolAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken) =>
        db.SchoolClasses.AsNoTracking()
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

    public async Task<string> SuggestStudentCodeAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken)
    {
        var target = await db.SchoolClasses
            .Include(c => c.SchoolBranch)
            .Include(c => c.AcademicYear)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId,
                cancellationToken);

        if (target is null) return string.Empty;

        var branchCode = target.SchoolBranch?.Code?.Trim() ?? "BR";
        var year = int.TryParse(target.AcademicYear.Name.Split('-')[0].Trim(), out var firstYear) ? firstYear : DateTime.UtcNow.Year;
        var cohort = (year % 100).ToString("D2");
        var prefix = $"{branchCode}-{cohort}";

        var existingCodes = await db.Users
            .AsNoTracking()
            .Where(s => s.Username.StartsWith(prefix))
            .Select(s => s.Username)
            .ToListAsync(cancellationToken);

        var maxSeq = 0;
        foreach (var c in existingCodes)
        {
            var suffix = c.Substring(prefix.Length);
            if (int.TryParse(suffix, out var num) && num > maxSeq)
            {
                maxSeq = num;
            }
        }

        var enrolledCount = await db.StudentEnrollments
            .AsNoTracking()
            .CountAsync(e => e.SchoolClass.SchoolBranchId == target.SchoolBranchId &&
                             e.SchoolClass.AcademicYearId == target.AcademicYearId, cancellationToken);

        if (enrolledCount > maxSeq) maxSeq = enrolledCount;

        var nextSeq = maxSeq + 1;
        var code = $"{prefix}{nextSeq:D4}";

        while (await db.Users.AnyAsync(s => s.Username == code, cancellationToken))
        {
            nextSeq++;
            code = $"{prefix}{nextSeq:D4}";
        }

        return code;
    }

    private IQueryable<StudentEnrollment> ListingEnrollments(ulong schoolId, ulong? academicYearId)
    {
        if (academicYearId is null) return DirectoryScopeHelper.CurrentEnrollments(db, schoolId);
        var inYear = db.StudentEnrollments.AsNoTracking().Where(e =>
            e.SchoolClass.SchoolBranch.SchoolId == schoolId && e.SchoolClass.AcademicYearId == academicYearId);
        // Resolve the last period before applying class filters. For a same-day transfer the
        // open period wins; class_id breaks remaining ties because DB v2 has no enrollment id.
        return inYear.Where(e => !inYear.Any(later => later.StudentId == e.StudentId &&
            (later.StartedOn > e.StartedOn || later.StartedOn == e.StartedOn &&
                (e.EndedOn != null && later.EndedOn == null ||
                 (e.EndedOn == null) == (later.EndedOn == null) && later.SchoolClassId > e.SchoolClassId))));
    }
}
