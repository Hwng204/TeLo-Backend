using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class MatrixReferenceRepository(
    ApplicationDbContext db,
    IMatrixRoleCatalog roleCatalog) : IMatrixReferenceRepository
{
    public async Task EnsureValidAsync(
        MatrixScope scope,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken,
        ulong? requiredBranchId = null)
    {
        if (requiredBranchId is not null && scope.BranchId != requiredBranchId)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Ngữ cảnh học thuật thuộc chi nhánh khác.");
        }

        if (!await ScopeExistsAsync(scope, cancellationToken))
        {
            throw InvalidReference("Không tìm thấy ngữ cảnh học thuật.");
        }

        var distinctLessonIds = lessonIds.Distinct().ToArray();

        if (distinctLessonIds.Length == 0)
        {
            return;
        }

        var validLessonCount = await LessonsOf(scope.BranchId, scope.GradeLevelId, scope.SubjectId)
            .Where(lesson => distinctLessonIds.Contains(lesson.Id))
            .CountAsync(cancellationToken);

        if (validLessonCount != distinctLessonIds.Length)
        {
            throw InvalidReference(
                "Mọi bài học trong ma trận phải thuộc chương của cùng phân hiệu, khối lớp và môn học với ngữ cảnh học thuật.");
        }
    }

    public async Task<MatrixExportInfo> GetExportInfoAsync(
        MatrixScope scope,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken)
    {
        var names = await db.SchoolBranches
            .AsNoTracking()
            .Where(branch => branch.Id == scope.BranchId)
            .Select(branch => new
            {
                School = branch.School.Name,
                Branch = branch.Name,
                Subject = db.Subjects.Where(subject => subject.Id == scope.SubjectId).Select(subject => subject.Name).FirstOrDefault(),
                Grade = db.GradeLevels.Where(grade => grade.Id == scope.GradeLevelId).Select(grade => grade.Name).FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        var lessons = await db.Lessons
            .AsNoTracking()
            .Where(lesson => lessonIds.Contains(lesson.Id))
            .Select(lesson => new { lesson.Id, lesson.Title, Chapter = lesson.Chapter.Title })
            .ToListAsync(cancellationToken);

        var label = names is null
            ? scope.Key.ToString()
            : ScopeLabel(names.Subject, names.Grade, names.School, names.Branch);

        return new MatrixExportInfo(
            label,
            lessons.ToDictionary(lesson => lesson.Id, lesson => $"Bài {lesson.Title} — Chương {lesson.Chapter}"));
    }

    // A scope exists through its ACTIVE chapters, the same rows the reference data lists.
    private Task<bool> ScopeExistsAsync(MatrixScope scope, CancellationToken cancellationToken) =>
        db.Chapters.AnyAsync(chapter =>
                chapter.Status == "ACTIVE" &&
                chapter.SchoolBranchId == scope.BranchId &&
                chapter.SubjectId == scope.SubjectId &&
                chapter.GradeLevelId == scope.GradeLevelId,
            cancellationToken);

    private static string ScopeLabel(string? subject, string? grade, string school, string branch) =>
        $"{subject} - {grade} - {school} / {branch}";

    // Bài dùng được cho một ngữ cảnh: bài ACTIVE của chương ACTIVE cùng phân hiệu, cùng khối và cùng môn của ngữ cảnh.
    // Bài đã lưu trữ chỉ còn hiện trong ma trận cũ (xuất file), không chọn mới được.
    private IQueryable<Domain.Entities.Academic.Lesson> LessonsOf(ulong branchId, ulong gradeLevelId, ulong subjectId)
    {
        return db.Lessons
            .AsNoTracking()
            .Where(lesson =>
                lesson.Status == "ACTIVE" &&
                lesson.Chapter.Status == "ACTIVE" &&
                lesson.Chapter.SchoolBranchId == branchId &&
                lesson.Chapter.GradeLevelId == gradeLevelId &&
                lesson.Chapter.SubjectId == subjectId);
    }

    private static MatrixDomainException InvalidReference(string message)
    {
        return new MatrixDomainException("InvalidReference", message);
    }

    public async Task EnsureAssignmentValidAsync(
        MatrixActor actor,
        ulong assignedToUserId,
        MatrixScope scope,
        CancellationToken cancellationToken)
    {
        var actorBranchId = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == actor.UserId && user.Status == "ACTIVE")
            .Select(user => user.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!await ScopeExistsAsync(scope, cancellationToken))
        {
            throw InvalidReference("Không tìm thấy ngữ cảnh học thuật.");
        }

        if (!actor.IsPrincipal &&
            (actorBranchId is null || actorBranchId.Value != scope.BranchId))
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Ngữ cảnh học thuật nằm ngoài chi nhánh của bạn.");
        }

        var assignee = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == assignedToUserId && user.Status == "ACTIVE",
                cancellationToken);

        if (assignee is null ||
            assignee.SchoolBranchId != scope.BranchId ||
            !await db.UserRoles.WhereEffective().AnyAsync(userRole =>
                userRole.UserId == assignedToUserId && roleCatalog.TeamLeadRoleCodes.Contains(userRole.Role.Code), cancellationToken))
        {
            throw new MatrixDomainException(
                "InvalidAssignee",
                "Người nhận phải là Tổ trưởng đang hoạt động thuộc cùng chi nhánh.");
        }
    }

    public async Task<MatrixReferenceModel> GetReferenceDataAsync(
        MatrixActor actor,
        ulong? academicContextId,
        CancellationToken cancellationToken)
    {
        var actorBranchId = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == actor.UserId && user.Status == "ACTIVE")
            .Select(user => user.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (actor.Role == MatrixActorRole.TeamLead && actorBranchId is null)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Tài khoản Tổ trưởng chưa được gán chi nhánh.");
        }

        if (actor.IsPrincipal)
        {
            actorBranchId = null;
        }
        else if (actor.Role == MatrixActorRole.Pht && actorBranchId is null)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Tài khoản PHT chưa được gán chi nhánh.");
        }

        // A key that is not a scope selects nothing (default scope has zero ids).
        MatrixScope? requested = academicContextId is null
            ? null
            : MatrixScope.TryParse(academicContextId.Value, out var parsed) ? parsed : default(MatrixScope);

        if (requested is { BranchId: > 0 } requestedScope &&
            actorBranchId is not null &&
            requestedScope.BranchId != actorBranchId)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Ngữ cảnh học thuật thuộc chi nhánh khác.");
        }

        // Scopes are the (branch, subject, grade) that have ACTIVE curriculum chapters; ids MatrixScope.Key cannot
        // encode are left out rather than failing the whole list.
        var chapters = db.Chapters
            .AsNoTracking()
            .Where(chapter =>
                chapter.Status == "ACTIVE" &&
                chapter.SchoolBranch.Status == "ACTIVE" &&
                chapter.SchoolBranch.School.Status == "ACTIVE" &&
                chapter.SubjectId < 1000 &&
                chapter.GradeLevelId < 1000);

        if (actorBranchId is not null)
        {
            chapters = chapters.Where(chapter => chapter.SchoolBranchId == actorBranchId.Value);
        }

        if (requested is { } scope)
        {
            chapters = chapters.Where(chapter =>
                chapter.SchoolBranchId == scope.BranchId &&
                chapter.SubjectId == scope.SubjectId &&
                chapter.GradeLevelId == scope.GradeLevelId);
        }

        var scopeRows = await chapters
            .Select(chapter => new
            {
                chapter.SchoolBranchId,
                chapter.SubjectId,
                chapter.GradeLevelId,
                SchoolName = chapter.SchoolBranch.School.Name,
                BranchName = chapter.SchoolBranch.Name,
                SubjectName = chapter.Subject.Name,
                GradeLevelName = chapter.GradeLevel.Name
            })
            .Distinct()
            .OrderBy(row => row.SchoolBranchId)
            .ThenBy(row => row.SubjectId)
            .ThenBy(row => row.GradeLevelId)
            .ToListAsync(cancellationToken);

        // Matrices have no year; pickers still show the ACTIVE one.
        var activeYear = await db.AcademicYears
            .AsNoTracking()
            .Where(year => year.Status == "ACTIVE")
            .OrderByDescending(year => year.Id)
            .Select(year => new { year.Id, year.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var contextOptions = scopeRows
            .Select(row => new MatrixAcademicContextOption(
                new MatrixScope(row.SchoolBranchId, row.SubjectId, row.GradeLevelId).Key,
                ScopeLabel(row.SubjectName, row.GradeLevelName, row.SchoolName, row.BranchName),
                activeYear?.Id ?? 0,
                row.SchoolBranchId,
                row.SubjectId,
                row.GradeLevelId,
                row.SubjectName,
                row.GradeLevelName,
                activeYear?.Name ?? ""))
            .ToArray();

        var semesters = activeYear is null
            ? []
            : await db.Semesters
                .AsNoTracking()
                .Where(semester => semester.AcademicYearId == activeYear.Id)
                .OrderBy(semester => semester.Order)
                .Select(semester => new MatrixSemesterOption(
                    semester.Id,
                    semester.AcademicYearId,
                    semester.Name,
                    null,
                    null))
                .ToListAsync(cancellationToken);

        // Bài học chỉ trả về khi đã chọn một phạm vi cụ thể.
        var selected = requested is null ? null : contextOptions.SingleOrDefault();
        var lessons = selected is null
            ? []
            : await LessonsOf(selected.SchoolBranchId, selected.GradeLevelId, selected.SubjectId)
                .OrderBy(lesson => lesson.Chapter.Number)
                .ThenBy(lesson => lesson.ChapterId)
                .ThenBy(lesson => lesson.Number)
                .ThenBy(lesson => lesson.Id)
                .Select(lesson => new MatrixLessonOption(
                    lesson.Id,
                    selected.Id,
                    lesson.ChapterId,
                    lesson.Title,
                    lesson.Number,
                    lesson.Number.ToString(),
                    lesson.Chapter.Number.ToString(),
                    lesson.Chapter.Title))
                .ToListAsync(cancellationToken);

        var effectiveTeamLeadUsers = db.UserRoles.WhereEffective()
            .Where(userRole => roleCatalog.TeamLeadRoleCodes.Contains(userRole.Role.Code))
            .Select(userRole => userRole.UserId);
        var teamLeads = actor.Role == MatrixActorRole.Pht
            ? await db.Users
                .AsNoTracking()
                .Where(user =>
                    user.Status == "ACTIVE" &&
                    (actorBranchId == null || user.SchoolBranchId == actorBranchId.Value) &&
                    effectiveTeamLeadUsers.Contains(user.Id))
                .OrderBy(user => user.FullName)
                .Select(user => new MatrixTeamLeadOption(
                    user.Id,
                    user.Username,
                    user.FullName,
                    user.SchoolBranchId))
                .ToListAsync(cancellationToken)
            : [];

        return new MatrixReferenceModel(contextOptions, semesters, lessons, teamLeads);
    }
}
