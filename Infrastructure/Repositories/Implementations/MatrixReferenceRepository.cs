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
        ulong academicContextId,
        ulong? semesterId,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken,
        ulong? requiredBranchId = null)
    {
        if (academicContextId == 0)
        {
            throw InvalidReference("Ngữ cảnh học thuật là bắt buộc.");
        }

        var context = await db.AcademicContexts
            .AsNoTracking()
            .Where(item => item.Id == academicContextId)
            .Select(item => new
            {
                item.AcademicYearId,
                item.TextbookId,
                item.SchoolBranchId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            throw InvalidReference("Không tìm thấy ngữ cảnh học thuật.");
        }

        if (requiredBranchId is not null && context.SchoolBranchId != requiredBranchId)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Ngữ cảnh học thuật thuộc chi nhánh khác.");
        }

        if (semesterId is not null)
        {
            if (semesterId == 0)
            {
                throw InvalidReference("Học kỳ không hợp lệ.");
            }

            var semesterYearId = await db.Semesters
                .AsNoTracking()
                .Where(item => item.Id == semesterId.Value)
                .Select(item => (ulong?)item.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);

            if (semesterYearId is null)
            {
                throw InvalidReference("Không tìm thấy học kỳ.");
            }

            if (semesterYearId != context.AcademicYearId)
            {
                throw InvalidReference(
                    "Học kỳ không thuộc năm học của ngữ cảnh học thuật.");
            }
        }

        var distinctLessonIds = lessonIds.Distinct().ToArray();

        if (distinctLessonIds.Length == 0)
        {
            return;
        }

        var validLessonCount = await db.TextbookLessons
            .AsNoTracking()
            .Where(lesson =>
                distinctLessonIds.Contains(lesson.Id) &&
                lesson.Chapter.TextbookId == context.TextbookId)
            .CountAsync(cancellationToken);

        if (validLessonCount != distinctLessonIds.Length)
        {
            throw InvalidReference(
                "Mọi bài học trong ma trận phải thuộc sách giáo khoa của ngữ cảnh học thuật.");
        }
    }

    public async Task<MatrixExportInfo> GetExportInfoAsync(
        ulong academicContextId,
        ulong? semesterId,
        IReadOnlyCollection<ulong> lessonIds,
        CancellationToken cancellationToken)
    {
        var context = await db.AcademicContexts
            .AsNoTracking()
            .Where(item => item.Id == academicContextId)
            .Select(item => new
            {
                Subject = item.Subject.Name,
                Grade = item.GradeLevel.Name,
                Year = item.AcademicYear.Name,
                School = item.School.Name,
                Branch = item.SchoolBranch.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        var semesterName = semesterId is null
            ? null
            : await db.Semesters
                .AsNoTracking()
                .Where(item => item.Id == semesterId.Value)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken);

        var lessons = await db.TextbookLessons
            .AsNoTracking()
            .Where(lesson => lessonIds.Contains(lesson.Id))
            .Select(lesson => new { lesson.Id, lesson.Title, Chapter = lesson.Chapter.Title })
            .ToListAsync(cancellationToken);

        var label = context is null
            ? academicContextId.ToString()
            : $"{context.Subject} - {context.Grade} - {context.Year} - {context.School} / {context.Branch}";

        return new MatrixExportInfo(
            label,
            semesterName,
            lessons.ToDictionary(lesson => lesson.Id, lesson => $"{lesson.Chapter} / {lesson.Title}"));
    }

    private static MatrixDomainException InvalidReference(string message)
    {
        return new MatrixDomainException("InvalidReference", message);
    }

    public async Task EnsureAssignmentValidAsync(
        MatrixActor actor,
        ulong assignedToUserId,
        ulong academicContextId,
        ulong? semesterId,
        CancellationToken cancellationToken)
    {
        var actorBranchId = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == actor.UserId && user.Status == "ACTIVE")
            .Select(user => user.SchoolBranchId)
            .SingleOrDefaultAsync(cancellationToken);

        var context = await db.AcademicContexts
            .AsNoTracking()
            .Where(item => item.Id == academicContextId)
            .Select(item => new { item.AcademicYearId, item.SchoolBranchId })
            .SingleOrDefaultAsync(cancellationToken);

        if (context is null)
        {
            throw new MatrixDomainException(
                "InvalidReference",
                "Không tìm thấy ngữ cảnh học thuật.");
        }

        if (!actor.IsPrincipal &&
            (actorBranchId is null || actorBranchId.Value != context.SchoolBranchId))
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Ngữ cảnh học thuật nằm ngoài chi nhánh của bạn.");
        }

        await EnsureSemesterAsync(
            context.AcademicYearId,
            semesterId,
            cancellationToken);

        var assignee = await db.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(
                user => user.Id == assignedToUserId && user.Status == "ACTIVE",
                cancellationToken);

        if (assignee is null ||
            assignee.SchoolBranchId != context.SchoolBranchId ||
            !assignee.UserRoles.Any(userRole =>
                roleCatalog.IsTeamLead(userRole.Role.Code)))
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

        if (academicContextId is not null && actorBranchId is not null)
        {
            var requestedBranchId = await db.AcademicContexts
                .AsNoTracking()
                .Where(context => context.Id == academicContextId.Value)
                .Select(context => (ulong?)context.SchoolBranchId)
                .SingleOrDefaultAsync(cancellationToken);

            if (requestedBranchId is not null && requestedBranchId != actorBranchId)
            {
                throw new MatrixDomainException(
                    "Forbidden",
                    "Ngữ cảnh học thuật thuộc chi nhánh khác.");
            }
        }

        var contexts = db.AcademicContexts
            .AsNoTracking()
            .Where(context =>
                context.AcademicYear.Status == "ACTIVE" &&
                context.School.Status == "ACTIVE" &&
                context.SchoolBranch.Status == "ACTIVE");

        if (actorBranchId is not null)
        {
            contexts = contexts.Where(context => context.SchoolBranchId == actorBranchId.Value);
        }

        if (academicContextId is not null)
        {
            contexts = contexts.Where(context => context.Id == academicContextId.Value);
        }

        var contextRows = await contexts
            .Select(context => new
            {
                context.Id,
                context.AcademicYearId,
                context.SchoolBranchId,
                context.TextbookId,
                context.SubjectId,
                context.GradeLevelId,
                AcademicYearName = context.AcademicYear.Name,
                SchoolName = context.School.Name,
                BranchName = context.SchoolBranch.Name,
                SubjectName = context.Subject.Name,
                GradeLevelName = context.GradeLevel.Name,
                TextbookTitle = context.Textbook.Title
            })
            .OrderBy(context => context.Id)
            .ToListAsync(cancellationToken);

        var contextOptions = contextRows
            .Select(context => new MatrixAcademicContextOption(
                context.Id,
                $"{context.SubjectName} - {context.GradeLevelName} - {context.AcademicYearName} - {context.SchoolName} / {context.BranchName}",
                context.AcademicYearId,
                context.SchoolBranchId,
                context.TextbookId,
                context.SubjectId,
                context.GradeLevelId,
                context.TextbookTitle,
                context.SubjectName,
                context.GradeLevelName,
                context.AcademicYearName))
            .ToArray();

        var academicYearIds = contextRows
            .Select(context => context.AcademicYearId)
            .Distinct()
            .ToArray();
        var semesters = await db.Semesters
            .AsNoTracking()
            .Where(semester =>
                academicYearIds.Contains(semester.AcademicYearId) &&
                semester.AcademicYear.Status == "ACTIVE")
            .OrderBy(semester => semester.StartDate)
            .Select(semester => new MatrixSemesterOption(
                semester.Id,
                semester.AcademicYearId,
                semester.Name,
                semester.StartDate,
                semester.EndDate))
            .ToListAsync(cancellationToken);

        var textbookIds = academicContextId is null
            ? Array.Empty<ulong>()
            : contextRows.Select(context => context.TextbookId).Distinct().ToArray();
        var lessonRows = await db.TextbookLessons
            .AsNoTracking()
            .Where(lesson => textbookIds.Contains(lesson.Chapter.TextbookId))
            .OrderBy(lesson => lesson.Chapter.SortOrder)
            .ThenBy(lesson => lesson.SortOrder)
            .Select(lesson => new
            {
                lesson.Id,
                TextbookId = lesson.Chapter.TextbookId,
                lesson.ChapterId,
                lesson.Title,
                lesson.SortOrder
            })
            .ToListAsync(cancellationToken);
        var lessons = lessonRows
            .Select(lesson => new MatrixLessonOption(
                lesson.Id,
                contextRows.First(context => context.TextbookId == lesson.TextbookId).Id,
                lesson.ChapterId,
                lesson.Title,
                lesson.SortOrder))
            .ToArray();

        var teamLeads = actor.Role == MatrixActorRole.Pht
            ? await db.Users
                .AsNoTracking()
                .Where(user =>
                    user.Status == "ACTIVE" &&
                    (actorBranchId == null || user.SchoolBranchId == actorBranchId.Value) &&
                    user.UserRoles.Any(userRole =>
                        roleCatalog.TeamLeadRoleCodes.Contains(userRole.Role.Code)))
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

    private async Task EnsureSemesterAsync(
        ulong academicYearId,
        ulong? semesterId,
        CancellationToken cancellationToken)
    {
        if (semesterId is null)
        {
            return;
        }

        var semesterYearId = await db.Semesters
            .AsNoTracking()
            .Where(semester => semester.Id == semesterId.Value)
            .Select(semester => (ulong?)semester.AcademicYearId)
            .SingleOrDefaultAsync(cancellationToken);

        if (semesterYearId is null || semesterYearId.Value != academicYearId)
        {
            throw new MatrixDomainException(
                "InvalidReference",
                "Học kỳ không thuộc năm học của ngữ cảnh học thuật.");
        }
    }
}
