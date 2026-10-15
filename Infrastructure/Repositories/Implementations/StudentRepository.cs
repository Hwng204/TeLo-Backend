using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class StudentRepository(ApplicationDbContext db) : IStudentRepository
{
    private const int DuplicateKeyErrorNumber = 1062;

    // ─── READ OPERATIONS ────────────────────────────────────────────────────────

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

        // An explicit year includes completed historical enrollments. Omitted year retains
        // the existing directory behavior for school-side callers.
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

            var matchingStudentIds = current.Select(e => e.StudentId);
            students = students.Where(s => matchingStudentIds.Contains(s.Id));
        }

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            students = students.Where(s => s.Code.Contains(search) || s.FullName.Contains(search));
        }

        if (filter.Status is { } statusFilter)
        {
            students = students.Where(s => s.Status == statusFilter);
        }

        var total = await students.CountAsync(cancellationToken);
        var page = await students
            .OrderBy(s => s.FullName).ThenBy(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(s => new
            {
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.Status
            })
            .ToListAsync(cancellationToken);

        var ids = page.Select(s => s.Id).ToArray();
        var currentRows = await ListingEnrollments(schoolId, filter.AcademicYearId)
            .Where(e => ids.Contains(e.StudentId))
            .Select(e => new
            {
                e.StudentId,
                EnrollmentId = e.Id,
                ClassId = e.SchoolClassId,
                ClassName = e.SchoolClass.Name,
                e.SchoolClass.GradeLevelId,
                GradeLevelName = e.SchoolClass.GradeLevel.Name,
                e.SchoolClass.SchoolBranchId,
                SchoolBranchName = e.SchoolClass.SchoolBranch.Name
            })
            .ToListAsync(cancellationToken);

        var byStudent = currentRows.ToDictionary(r => r.StudentId);
        var items = page.Select(s =>
        {
            byStudent.TryGetValue(s.Id, out var currentEnrollment);
            return new StudentDirectoryRow(
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender,
                currentEnrollment?.GradeLevelId, currentEnrollment?.GradeLevelName,
                currentEnrollment?.ClassId, currentEnrollment?.ClassName,
                currentEnrollment?.SchoolBranchId, currentEnrollment?.SchoolBranchName,
                s.Status);
        }).ToList();

        return DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Ok(
            new DirectoryRowsPage<StudentDirectoryRow>(items, total));
    }

    public async Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var (status, resolved) = await DirectoryScopeHelper.ResolveScopeAsync(db, scope, cancellationToken);
        if (resolved is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(status);
        }

        var schoolId = resolved.SchoolId;
        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == studentId &&
                s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId))
            .Select(s => new
            {
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        if (resolved.HomeroomClassIds is { } homeroomIds &&
            !await db.StudentEnrollments.AsNoTracking().AnyAsync(
                e => e.StudentId == studentId && homeroomIds.Contains(e.SchoolClassId), cancellationToken))
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var history = await BuildHistoryAsync(schoolId, studentId, cancellationToken);
        return DirectoryReadResult<StudentDetailRow>.Ok(new StudentDetailRow(
            student.Id, student.Code, student.FullName, student.DateOfBirth, student.Gender,
            student.Status, history));
    }

    public async Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope scope,
        ulong studentId,
        ulong classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (status, resolved) = await DirectoryScopeHelper.ResolveScopeAsync(db, scope, cancellationToken);
        if (resolved is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(status);
        }

        var schoolId = resolved.SchoolId;
        var enrolled = await db.StudentEnrollments.AsNoTracking()
            .AnyAsync(
                e => e.StudentId == studentId &&
                    e.SchoolClassId == classId &&
                    e.SchoolClass.SchoolBranch.SchoolId == schoolId,
                cancellationToken);

        if (!enrolled)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(DirectoryReadStatus.NotFound);
        }

        if (resolved.HomeroomClassIds is { } homeroomIds && !homeroomIds.Contains(classId))
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(DirectoryReadStatus.NotFound);
        }

        var attempts = db.ExamAttempts.AsNoTracking()
            .Where(a =>
                a.ExamRegistration.StudentId == studentId &&
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.SchoolBranch.SchoolId == schoolId &&
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Status == "PUBLISHED" &&
                a.SubmittedAt != null &&
                a.TotalScore != null);

        var total = await attempts.CountAsync(cancellationToken);
        var rows = await attempts
            .OrderByDescending(a => a.SubmittedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new StudentScoreRow(
                a.Id,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.ExamId,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.Name,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.SemesterId,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.Semester.Name,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.SubjectId,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Subject.Name,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.Exam.StartDate,
                a.TotalScore!.Value,
                a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject.ResultPublishedAt ?? a.SubmittedAt!.Value))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Ok(
            new DirectoryRowsPage<StudentScoreRow>(rows, total));
    }

    // ─── WRITE OPERATIONS ───────────────────────────────────────────────────────

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

        var studentCode = command.Code?.Trim();
        if (string.IsNullOrEmpty(studentCode))
        {
            studentCode = await SuggestStudentCodeAsync(command.SchoolId, command.SchoolClassId, cancellationToken);
            if (string.IsNullOrEmpty(studentCode))
            {
                return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
            }
        }
        else if (await db.Students.AnyAsync(s => s.Code == studentCode, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        var student = new Student
        {
            Code = studentCode,
            FullName = command.FullName,
            DateOfBirth = command.DateOfBirth,
            Gender = command.Gender,
            Status = command.Status,
            Enrollments =
            {
                new StudentEnrollment
                {
                    SchoolClassId = target.Id,
                    AcademicYearId = target.AcademicYearId,
                    Status = StudentEnrollmentStatusCodes.Active,
                    StartedOn = DateOnly.FromDateTime(DateTime.UtcNow)
                }
            }
        };
        db.Students.Add(student);

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
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
        var year = target.AcademicYear?.StartDate.Year ?? DateTime.UtcNow.Year;
        var cohort = (year % 100).ToString("D2");
        var prefix = $"{branchCode}-{cohort}";

        var existingCodes = await db.Students
            .AsNoTracking()
            .Where(s => s.Code.StartsWith(prefix))
            .Select(s => s.Code)
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
                             e.AcademicYearId == target.AcademicYearId, cancellationToken);

        if (enrolledCount > maxSeq) maxSeq = enrolledCount;

        var nextSeq = maxSeq + 1;
        var code = $"{prefix}{nextSeq:D4}";

        while (await db.Students.AnyAsync(s => s.Code == code, cancellationToken))
        {
            nextSeq++;
            code = $"{prefix}{nextSeq:D4}";
        }

        return code;
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

        var reactivating = student.Status == StudentStatusCodes.Inactive &&
            command.Status != StudentStatusCodes.Inactive;
        if ((student.Code != command.Code || reactivating) &&
            await db.Students.AnyAsync(
                s => s.Code == command.Code && s.Id != student.Id, cancellationToken))
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.DuplicateStudentCode);
        }

        student.Code = command.Code;
        student.FullName = command.FullName;
        student.DateOfBirth = command.DateOfBirth;
        student.Gender = command.Gender;
        student.Status = command.Status;

        if (command.SchoolClassId is { } newClassId)
        {
            var target = await FindClassInSchoolAsync(
                command.SchoolId, newClassId, cancellationToken);
            if (target is null || target.Status != SchoolClassStatusCodes.Active)
            {
                return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.ClassNotFound);
            }

            var activeYearId = target.AcademicYearId;
            var current = student.Enrollments
                .SingleOrDefault(e => e.AcademicYearId == activeYearId &&
                    e.Status == StudentEnrollmentStatusCodes.Active);

            if (current is null)
            {
                student.Enrollments.Add(new StudentEnrollment
                {
                    SchoolClassId = target.Id,
                    AcademicYearId = activeYearId,
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

        var activeYearId = target.AcademicYearId;
        var current = student.Enrollments
            .SingleOrDefault(e => e.AcademicYearId == activeYearId &&
                e.Status == StudentEnrollmentStatusCodes.Active);
        if (current is null)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.StudentNotEnrolledInYear);
        }

        if (current.SchoolClassId == target.Id)
        {
            return DirectoryWriteResult<ulong>.Ok(student.Id);
        }

        var effectiveOn = command.EffectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveOn < current.StartedOn)
        {
            return DirectoryWriteResult<ulong>.Fail(DirectoryWriteStatus.InvalidEffectiveDate);
        }

        current.Status = StudentEnrollmentStatusCodes.TransferredOut;
        current.EndedOn = effectiveOn;

        student.Enrollments.Add(new StudentEnrollment
        {
            SchoolClassId = target.Id,
            AcademicYearId = activeYearId,
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

        if (student.Status == StudentStatusCodes.Inactive)
        {
            return DirectoryWriteResult<ulong>.Ok(student.Id);
        }

        student.Status = StudentStatusCodes.Inactive;
        foreach (var enrollment in student.Enrollments.Where(e =>
            e.Status == StudentEnrollmentStatusCodes.Active))
        {
            enrollment.Status = StudentEnrollmentStatusCodes.TransferredOut;
            enrollment.EndedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        return await SaveAsync(
            () => student.Id, DirectoryWriteStatus.DuplicateStudentCode, cancellationToken);
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<StudentEnrollmentRow>> BuildHistoryAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var rows = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolClass.SchoolBranch.SchoolId == schoolId)
            .OrderByDescending(e => e.AcademicYear.StartDate)
            .ThenByDescending(e => e.Id)
            .Select(e => new
            {
                e.AcademicYearId,
                AcademicYearName = e.AcademicYear.Name,
                AcademicYearStatus = e.AcademicYear.Status,
                e.SchoolClass.GradeLevelId,
                GradeLevelName = e.SchoolClass.GradeLevel.Name,
                e.SchoolClassId,
                ClassName = e.SchoolClass.Name,
                e.SchoolClass.SchoolBranchId,
                SchoolBranchName = e.SchoolClass.SchoolBranch.Name,
                e.Status
            })
            .ToListAsync(cancellationToken);

        var classIds = rows.Select(r => r.SchoolClassId).Distinct().ToArray();
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.ClassId != null && classIds.Contains(t.ClassId.Value))
            .Select(t => new { ClassId = t.ClassId!.Value, t.Id, t.User.FullName })
            .ToListAsync(cancellationToken);
        var teacherByClass = teachers.ToDictionary(t => t.ClassId);

        return rows.Select(r =>
        {
            teacherByClass.TryGetValue(r.SchoolClassId, out var homeroom);
            return new StudentEnrollmentRow(
                r.AcademicYearId, r.AcademicYearName, r.AcademicYearStatus,
                r.GradeLevelId, r.GradeLevelName,
                r.SchoolClassId, r.ClassName,
                r.SchoolBranchId, r.SchoolBranchName,
                homeroom?.Id, homeroom?.FullName,
                r.Status);
        }).ToList();
    }

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
            .Include(s => s.Enrollments)
            .SingleOrDefaultAsync(
                s => s.Id == studentId &&
                    s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId),
                cancellationToken);

    private Task<bool> SchoolExistsAsync(ulong schoolId, CancellationToken cancellationToken) =>
        db.Schools.AsNoTracking().AnyAsync(s => s.Id == schoolId, cancellationToken);

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

    private IQueryable<StudentEnrollment> ListingEnrollments(ulong schoolId, ulong? academicYearId)
    {
        if (academicYearId is null) return DirectoryScopeHelper.CurrentEnrollments(db, schoolId);
        var inYear = db.StudentEnrollments.AsNoTracking().Where(e =>
            e.AcademicYearId == academicYearId && e.SchoolClass.SchoolBranch.SchoolId == schoolId);
        // Pick the latest class period before applying class/grade filters, so a transfer
        // does not match a former class while displaying the student's later class.
        return inYear.Where(e => !inYear.Any(later => later.StudentId == e.StudentId &&
            (later.StartedOn > e.StartedOn || (later.StartedOn == e.StartedOn && later.Id > e.Id))));
    }
}
