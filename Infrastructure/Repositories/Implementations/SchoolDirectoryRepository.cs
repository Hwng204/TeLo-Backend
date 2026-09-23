using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class SchoolDirectoryRepository(ApplicationDbContext db) : ISchoolDirectoryRepository
{
    private const string ActiveYear = "ACTIVE";

    private sealed record ResolvedScope(ulong SchoolId, string? ProvinceCode);

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

    public async Task<DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>> ListStudentsAsync(
        StudentDirectoryFilter filter,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await ResolveScopeAsync(filter.Scope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var students = db.Students.AsNoTracking()
            .Where(s => s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId));

        // Class/grade/branch filters apply to the current (active-year) enrollment inside this school.
        if (filter.ClassId is not null || filter.GradeLevelId is not null || filter.SchoolBranchId is not null)
        {
            var current = CurrentEnrollments(schoolId);
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
            students = students.Where(s => s.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            students = students.Where(s =>
                s.Code.Contains(filter.Search) || s.FullName.Contains(filter.Search));
        }

        var total = await students.CountAsync(cancellationToken);
        var page = await students
            .OrderBy(s => s.FullName).ThenBy(s => s.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(s => new
            {
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.Status, s.AdmissionDate
            })
            .ToListAsync(cancellationToken);

        var ids = page.Select(s => s.Id).ToArray();
        var currentRows = await CurrentEnrollments(schoolId)
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
        var currentByStudent = currentRows
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EnrollmentId).First());

        var items = page.Select(s =>
        {
            currentByStudent.TryGetValue(s.Id, out var c);
            return new StudentDirectoryRow(
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender,
                c?.GradeLevelId, c?.GradeLevelName, c?.ClassId, c?.ClassName,
                c?.SchoolBranchId, c?.SchoolBranchName, s.Status, s.AdmissionDate);
        }).ToList();

        return DirectoryReadResult<DirectoryRowsPage<StudentDirectoryRow>>.Ok(
            new DirectoryRowsPage<StudentDirectoryRow>(items, total));
    }

    public async Task<DirectoryReadResult<StudentDetailRow>> GetStudentAsync(
        DirectoryScope requestedScope,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await ResolveScopeAsync(requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var student = await db.Students.AsNoTracking()
            .Where(s => s.Id == studentId &&
                s.Enrollments.Any(e => e.SchoolClass.SchoolBranch.SchoolId == schoolId))
            .Select(s => new
            {
                s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.AdmissionDate, s.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (student is null)
        {
            return DirectoryReadResult<StudentDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var history = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolClass.SchoolBranch.SchoolId == schoolId)
            .OrderByDescending(e => e.AcademicYear.StartDate).ThenByDescending(e => e.Id)
            .Select(e => new
            {
                e.AcademicYearId,
                AcademicYearName = e.AcademicYear.Name,
                AcademicYearStatus = e.AcademicYear.Status,
                e.SchoolClass.GradeLevelId,
                GradeLevelName = e.SchoolClass.GradeLevel.Name,
                ClassId = e.SchoolClassId,
                ClassName = e.SchoolClass.Name,
                e.SchoolClass.SchoolBranchId,
                SchoolBranchName = e.SchoolClass.SchoolBranch.Name,
                e.Status
            })
            .ToListAsync(cancellationToken);

        var teachers = await LoadTeachersAsync(
            history.Select(h => h.ClassId).Distinct().ToArray(), cancellationToken);
        var rows = history.Select(h =>
        {
            var hasTeacher = teachers.TryGetValue(h.ClassId, out var teacher);
            return new StudentEnrollmentRow(
                h.AcademicYearId, h.AcademicYearName, h.AcademicYearStatus,
                h.GradeLevelId, h.GradeLevelName, h.ClassId, h.ClassName,
                h.SchoolBranchId, h.SchoolBranchName,
                hasTeacher ? teacher.Id : null, hasTeacher ? teacher.Name : null, h.Status);
        }).ToList();

        return DirectoryReadResult<StudentDetailRow>.Ok(new StudentDetailRow(
            student.Id, student.Code, student.FullName, student.DateOfBirth, student.Gender,
            student.AdmissionDate, student.Status, rows));
    }

    public async Task<DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>> ListStudentScoresAsync(
        DirectoryScope requestedScope,
        ulong studentId,
        ulong classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await ResolveScopeAsync(requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        // One lookup proves the student, the class and the enrollment linking them are all in scope.
        var enrolled = await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SchoolClassId == classId &&
                e.SchoolClass.SchoolBranch.SchoolId == schoolId)
            .Select(e => new
            {
                e.AcademicYearId,
                e.SchoolClass.GradeLevelId,
                e.SchoolClass.SchoolBranchId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (enrolled is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Fail(
                DirectoryReadStatus.NotFound);
        }

        var scores = db.ExamAttempts.AsNoTracking()
            .Select(a => new
            {
                AttemptId = a.Id,
                a.TotalScore,
                a.ExamRegistration.StudentId,
                Subject = a.ExamRegistration.ExamSubjectGradeLevel.ExamSubject,
                a.ExamRegistration.ExamSubjectGradeLevel.GradeLevelId
            })
            .Where(x =>
                x.StudentId == studentId &&
                x.TotalScore != null &&
                x.Subject.ResultPublishedAt != null &&
                x.GradeLevelId == enrolled.GradeLevelId &&
                x.Subject.Exam.SchoolBranchId == enrolled.SchoolBranchId &&
                x.Subject.Exam.Semester.AcademicYearId == enrolled.AcademicYearId);

        var total = await scores.CountAsync(cancellationToken);
        var items = await scores
            .OrderByDescending(x => x.Subject.Exam.StartDate)
            .ThenBy(x => x.Subject.Subject.Name)
            .ThenBy(x => x.AttemptId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new StudentScoreRow(
                x.AttemptId,
                x.Subject.ExamId,
                x.Subject.Exam.Name,
                x.Subject.Exam.SemesterId,
                x.Subject.Exam.Semester.Name,
                x.Subject.SubjectId,
                x.Subject.Subject.Name,
                x.Subject.Exam.StartDate,
                x.TotalScore!.Value,
                x.Subject.ResultPublishedAt!.Value))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<DirectoryRowsPage<StudentScoreRow>>.Ok(
            new DirectoryRowsPage<StudentScoreRow>(items, total));
    }

    public async Task<DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>> ListClassesAsync(
        ClassDirectoryFilter filter,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await ResolveScopeAsync(filter.Scope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(status);
        }

        var academicYearId = filter.AcademicYearId
            ?? await ResolveActiveYearIdAsync(scope, cancellationToken);
        if (academicYearId is null)
        {
            return DirectoryReadResult<DirectoryRowsPage<ClassDirectoryRow>>.Fail(
                DirectoryReadStatus.ActiveAcademicYearNotFound);
        }

        var schoolId = scope.SchoolId;
        var yearId = academicYearId.Value;
        var classes = db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId && c.AcademicYearId == yearId);

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
            classes = classes.Where(c =>
                c.Code.Contains(filter.Search) || c.Name.Contains(filter.Search));
        }

        var total = await classes.CountAsync(cancellationToken);
        var page = await Project(classes
                .OrderBy(c => c.GradeLevel.Name).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize))
            .ToListAsync(cancellationToken);

        var teachers = await LoadTeachersAsync(
            page.Select(c => c.Id).ToArray(), cancellationToken);
        var items = page.Select(c => ToRow(c, teachers)).ToList();

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
        var (status, scope) = await ResolveScopeAsync(requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var projection = await Project(db.SchoolClasses.AsNoTracking()
                .Where(c => c.Id == classId && c.SchoolBranch.SchoolId == schoolId))
            .SingleOrDefaultAsync(cancellationToken);
        if (projection is null)
        {
            return DirectoryReadResult<ClassDetailRow>.Fail(DirectoryReadStatus.NotFound);
        }

        var teachers = await LoadTeachersAsync(new[] { classId }, cancellationToken);
        var roster = db.StudentEnrollments.AsNoTracking().Where(e => e.SchoolClassId == classId);
        var total = await roster.CountAsync(cancellationToken);
        var students = await roster
            .OrderBy(e => e.Student.FullName).ThenBy(e => e.StudentId)
            .Skip((rosterPage - 1) * rosterPageSize).Take(rosterPageSize)
            .Select(e => new ClassStudentRow(
                e.StudentId, e.Student.Code, e.Student.FullName, e.Student.DateOfBirth,
                e.Student.Gender, e.SchoolClass.Name, e.Student.Status))
            .ToListAsync(cancellationToken);

        return DirectoryReadResult<ClassDetailRow>.Ok(new ClassDetailRow(
            ToRow(projection, teachers),
            new DirectoryRowsPage<ClassStudentRow>(students, total)));
    }

    public async Task<DirectoryReadResult<DirectoryReferenceRows>> GetReferenceDataAsync(
        DirectoryScope requestedScope,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        var (status, scope) = await ResolveScopeAsync(requestedScope, cancellationToken);
        if (scope is null)
        {
            return DirectoryReadResult<DirectoryReferenceRows>.Fail(status);
        }

        var schoolId = scope.SchoolId;
        var schoolClasses = db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId);

        var branches = await db.SchoolBranches.AsNoTracking()
            .Where(b => b.SchoolId == schoolId && b.Status == "ACTIVE")
            .OrderBy(b => b.Name).ThenBy(b => b.Id)
            .Select(b => new DirectoryOptionRow(b.Id, b.Code, b.Name))
            .ToListAsync(cancellationToken);
        var grades = await db.GradeLevels.AsNoTracking()
            .Where(g => g.Status == "ACTIVE" && schoolClasses.Any(c => c.GradeLevelId == g.Id))
            .OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Select(g => new DirectoryOptionRow(g.Id, null, g.Name))
            .ToListAsync(cancellationToken);
        var years = await db.AcademicYears.AsNoTracking()
            .Where(y => schoolClasses.Any(c => c.AcademicYearId == y.Id))
            .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
            .Select(y => new DirectoryOptionRow(y.Id, y.Code, y.Name))
            .ToListAsync(cancellationToken);

        var yearId = academicYearId ?? await ResolveActiveYearIdAsync(scope, cancellationToken);
        var classes = yearId is null
            ? new List<DirectoryOptionRow>()
            : await schoolClasses
                .Where(c => c.AcademicYearId == yearId)
                .OrderBy(c => c.GradeLevel.Name).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => new DirectoryOptionRow(c.Id, c.Code, c.Name))
                .ToListAsync(cancellationToken);

        return DirectoryReadResult<DirectoryReferenceRows>.Ok(
            new DirectoryReferenceRows(branches, grades, years, classes));
    }

    private async Task<(DirectoryReadStatus Status, ResolvedScope? Scope)> ResolveScopeAsync(
        DirectoryScope requested,
        CancellationToken cancellationToken)
    {
        // Admin path: the route names the school, so the actor needs no branch of its own.
        if (requested.SchoolId is { } schoolId)
        {
            var school = await db.Schools.AsNoTracking()
                .Where(s => s.Id == schoolId)
                .Select(s => new ResolvedScope(s.Id, s.ProvinceCode))
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

        return actor.SchoolId is { } actorSchoolId
            ? (DirectoryReadStatus.Success, new ResolvedScope(actorSchoolId, actor.ProvinceCode))
            : (DirectoryReadStatus.SchoolScopeMissing, null);
    }

    private Task<ulong?> ResolveActiveYearIdAsync(
        ResolvedScope scope,
        CancellationToken cancellationToken) =>
        db.AcademicYears.AsNoTracking()
            .Where(y => y.Status == ActiveYear && y.ProvinceCode == scope.ProvinceCode)
            .OrderByDescending(y => y.StartDate)
            .Select(y => (ulong?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // A student can hold several rows in one year once a class transfer keeps the old period, so
    // only the ACTIVE row counts as "current". Without this a transferred student appears twice.
    private IQueryable<StudentEnrollment> CurrentEnrollments(ulong schoolId) =>
        db.StudentEnrollments.AsNoTracking().Where(e =>
            e.SchoolClass.SchoolBranch.SchoolId == schoolId &&
            e.AcademicYear.Status == ActiveYear &&
            e.Status == StudentEnrollmentStatusCodes.Active);

    private async Task<Dictionary<ulong, (ulong Id, string Name)>> LoadTeachersAsync(
        ulong[] classIds,
        CancellationToken cancellationToken)
    {
        var teachers = await db.Teachers.AsNoTracking()
            .Where(t => t.ClassId != null && classIds.Contains(t.ClassId.Value))
            .Select(t => new { ClassId = t.ClassId!.Value, t.Id, t.User.FullName })
            .ToListAsync(cancellationToken);
        return teachers.ToDictionary(t => t.ClassId, t => (t.Id, t.FullName));
    }

    private static IQueryable<ClassProjection> Project(IQueryable<SchoolClass> classes) =>
        classes.Select(c => new ClassProjection(
            c.Id, c.Code, c.Name, c.GradeLevelId, c.GradeLevel.Name,
            c.AcademicYearId, c.AcademicYear.Name, c.SchoolBranchId, c.SchoolBranch.Name,
            c.Enrollments.Count(e => e.Status == StudentEnrollmentStatusCodes.Active),
            c.Status));

    private static ClassDirectoryRow ToRow(
        ClassProjection c,
        IReadOnlyDictionary<ulong, (ulong Id, string Name)> teachers)
    {
        var hasTeacher = teachers.TryGetValue(c.Id, out var teacher);
        return new ClassDirectoryRow(
            c.Id, c.Code, c.Name, c.GradeLevelId, c.GradeLevelName,
            c.AcademicYearId, c.AcademicYearName, c.SchoolBranchId, c.SchoolBranchName,
            hasTeacher ? teacher.Id : null, hasTeacher ? teacher.Name : null,
            c.StudentCount, c.Status);
    }
}
