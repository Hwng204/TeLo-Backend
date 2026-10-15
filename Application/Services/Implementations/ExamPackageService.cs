using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services.Implement;

public sealed partial class ExamPackageService(ApplicationDbContext db, TimeProvider time, IMatrixRoleCatalog roles,
    IConfiguration configuration, ExamPackageStorage storage, QuestionAccess access)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private static QuestionException Missing() => new("EXAM_PACKAGE_NOT_FOUND", "Không tìm thấy đề hoặc bạn không có quyền xem.", 404);
    private static void Require(bool condition, string message, int status = 409)
    { if (!condition) throw new QuestionException(status == 403 ? "FORBIDDEN" : "EXAM_PACKAGE_LOCKED", message, status); }
    private static string Name(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 255)
            throw new QuestionException("VALIDATION_ERROR", "Tên đề là bắt buộc, tối đa 255 ký tự.", 422);
        return name.Trim();
    }
    private static string? Comment(string? comment, bool required = false)
    {
        if (comment?.Length > 2000 || required && string.IsNullOrWhiteSpace(comment))
            throw new QuestionException("VALIDATION_ERROR", required ? "Nhập lý do trả lại, tối đa 2.000 ký tự." : "Nhận xét tối đa 2.000 ký tự.");
        return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private Task<User> UserAsync(QuestionActor actor, CancellationToken ct) => access.UserAsync(actor, ct);

    private async Task<IQueryable<ExamSet>> ScopedAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await UserAsync(actor, ct);
        var query = db.ExamSets.AsQueryable();
        if (actor.Principal)
        {
            if (user.SchoolBranch is not null) query = query.Where(x => x.ExamMatrix.Branch.SchoolId == user.SchoolBranch.SchoolId);
        }
        else
        {
            Require(user.SchoolBranchId is not null, "Tài khoản chưa được gán phân hiệu.", 403);
            query = query.Where(x => x.ExamMatrix.BranchId == user.SchoolBranchId);
            if (!actor.Pht && !actor.TeamLead)
                query = query.Where(x => x.Status == "APPROVED" || x.ApprovedByUserId == actor.UserId);
        }
        return query;
    }

    private static IQueryable<ExamSet> Include(IQueryable<ExamSet> query) => query.Include(x => x.Task)
        .Include(x => x.ExamMatrix).ThenInclude(x => x.Subject)
        .Include(x => x.ExamMatrix).ThenInclude(x => x.GradeLevel);
    private static bool Manager(QuestionActor actor) => actor.Pht;
    private static bool Reviewing(QuestionActor actor, ExamSet set, ExamPackageState state) =>
        state.ReviewAssigned && (actor.Teacher || actor.TeamLead) && set.ApprovedByUserId == actor.UserId;
    private IQueryable<Question> Candidates(ExamMatrix matrix) => QuestionBankService.Include(db.Questions.Where(q =>
        q.Status == "APPROVED" && q.BankType == "exam" && q.Lesson.Chapter.SchoolBranchId == matrix.BranchId &&
        q.Lesson.Chapter.SubjectId == matrix.SubjectId && q.Lesson.Chapter.GradeLevelId == matrix.GradeLevelId));
    private Task<bool> ReferencedAsync(ulong id, CancellationToken ct) => db.ExamSubjects.AnyAsync(x => x.ExamSetId == id, ct);
    private Task<bool> UnfinishedExamAsync(ulong id, CancellationToken ct) => db.ExamSubjects.AnyAsync(link => link.ExamSetId == id &&
        db.Exams.Any(exam => exam.Id == link.ExamId && exam.Status != "COMPLETED" && exam.Status != "CANCELLED"), ct);

    private async Task<ExamPackageListItem> ItemAsync(ExamSet set, ExamPackageState state, CancellationToken ct)
    {
        var ids = new ulong?[] { state.CreatorId, state.ReviewAssigned ? set.ApprovedByUserId : null };
        var people = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return Item(set, state, people);
    }

    private static ExamPackageListItem Item(ExamSet set, ExamPackageState state, IReadOnlyDictionary<ulong, string> people)
    {
        return new(set.Id, set.Name, set.Status, state.Purpose, set.ExamMatrixId, set.ExamMatrix.Name,
            set.ExamMatrix.Subject.Name, set.ExamMatrix.GradeLevel.Name, state.AcademicYearName, state.Rows.Count,
            people.GetValueOrDefault(state.CreatorId, ""), state.ReviewAssigned && set.ApprovedByUserId is { } reviewer ? people.GetValueOrDefault(reviewer) : null,
            state.CreatedAt, state.History.LastOrDefault()?.At, state.FinalizedAt);
    }

    public async Task<DirectoryPage<ExamPackageListItem>> ListAsync(QuestionActor actor, ExamPackageQuery request, CancellationToken ct)
    {
        if (request.Page is < 1 or > 100000 || request.PageSize is < 1 or > 100 || request.Keyword?.Length > 255 ||
            request.Status is not (null or "" or "DRAFT" or "IN_REVIEW" or "REVIEWED" or "APPROVED") || request.Purpose is not (null or "" or "exam" or "practice"))
            throw new QuestionException("VALIDATION_ERROR", "Bộ lọc đề thi không hợp lệ.");
        var query = (await ScopedAsync(actor, ct)).Where(x => x.Questions.Any());
        if (!string.IsNullOrWhiteSpace(request.Keyword)) query = query.Where(x => x.Name.Contains(request.Keyword.Trim()));
        if (!string.IsNullOrEmpty(request.Status)) query = query.Where(x => x.Status == request.Status);
        if (request.SubjectId is { } subject) query = query.Where(x => x.ExamMatrix.SubjectId == subject);
        if (request.GradeLevelId is { } grade) query = query.Where(x => x.ExamMatrix.GradeLevelId == grade);
        query = ExamPackageIndex.Filter(query, db, request.Purpose, request.AcademicYearId);
        var total = await query.CountAsync(ct);
        var sets = await Include(query).AsNoTracking().OrderByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        var states = await storage.ReadManyAsync(sets, ct);
        var ids = states.Values.Select(x => x.CreatorId).Concat(sets.Where(x => x.ApprovedByUserId.HasValue).Select(x => x.ApprovedByUserId!.Value)).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var items = sets.Where(set => !states[set.Id].Deleted).Select(set => Item(set, states[set.Id], people)).ToArray();
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<ExamPackageOptions> OptionsAsync(QuestionActor actor, CancellationToken ct, ulong? packageId = null)
    {
        var user = await UserAsync(actor, ct); var excluded = actor.UserId;
        if (packageId is { } id)
        {
            var set = await (await ScopedAsync(actor, ct)).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
            var state = (await storage.ReadAsync(set, ct)).State;
            Require((await storage.MatrixAsync(set.ExamMatrixId, ct)).BranchId == user.SchoolBranchId, "Chỉ giao review đề thuộc phân hiệu của bạn.", 403);
            excluded = state.CreatorId;
        }
        var matrices = db.ExamMatrices.AsNoTracking().Where(x => x.Status == "APPROVED" || x.Status == "ARCHIVED");
        if (actor.Principal && user.SchoolBranch is not null) matrices = matrices.Where(x => x.Branch.SchoolId == user.SchoolBranch.SchoolId);
        else if (!actor.Principal) matrices = matrices.Where(x => x.BranchId == user.SchoolBranchId);
        var year = await db.AcademicYears.AsNoTracking().Where(x => x.Status == "ACTIVE").FirstOrDefaultAsync(ct);
        var options = await matrices.OrderBy(x => x.Name).Select(x => new ExamPackageOption(x.Id, x.Name, x.Subject.Name,
            x.GradeLevel.Name, year == null ? "" : year.Name, year == null ? 0 : year.Id, x.SubjectId, x.GradeLevelId, x.Status)).ToArrayAsync(ct);
        var codes = TaskService.TeacherRoleCodes(configuration).Concat(roles.TeamLeadRoleCodes).ToArray();
        var reviewers = actor.Pht ? await db.Users.AsNoTracking().Where(x => x.Status == "ACTIVE" && x.SchoolBranchId == user.SchoolBranchId &&
            x.Id != excluded && x.UserRoles.Any(grant => grant.Role.Status == "ACTIVE" && codes.Contains(grant.Role.Code)))
            .OrderBy(x => x.FullName).Select(x => new TaskPersonOption(x.Id, x.FullName, x.Username)).ToArrayAsync(ct) : [];
        var years = await db.AcademicYears.AsNoTracking().OrderByDescending(x => x.Name)
            .Select(x => new BankFilterOption(x.Id, x.Name, null)).ToArrayAsync(ct);
        return new(actor.Pht && user.SchoolBranchId is not null, options, reviewers, years);
    }

    public async Task<ExamPackageDetail> GetAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var set = await Include(await ScopedAsync(actor, ct)).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        var (state, version) = await storage.ReadAsync(set, ct);
        if (state.Deleted) throw Missing();
        var writable = (await UserAsync(actor, ct)).SchoolBranchId == set.ExamMatrix.BranchId;
        var manager = writable && Manager(actor) && state.Purpose == "exam";
        var reviewing = writable && Reviewing(actor, set, state) && set.Status == "IN_REVIEW" && state.Purpose == "exam";
        var unused = !await ReferencedAsync(id, ct) && !await db.ExamVariants.AnyAsync(x => x.ExamSetId == id, ct);
        var actors = state.History.Select(x => x.ActorId).Append(state.FinalizedBy ?? 0).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(x => actors.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var history = state.History.Select((entry, index) => new ExamPackageHistoryItem((ulong)index + 1, index + 1,
            entry.Action, people.GetValueOrDefault(entry.ActorId, ""), entry.At, entry.Comment, entry.OldId, entry.NewId)).Reverse().ToArray();
        return new(await ItemAsync(set, state, ct), version, set.ExamMatrix.TotalScore, state.ReviewAssigned ? set.TaskId : null,
            state.ReviewAssigned ? set.ApprovedByUserId : null, state.ReviewAssigned ? set.Task?.DueAt : null,
            state.FinalizedBy is { } approver ? people.GetValueOrDefault(approver) : null, state.SourceId,
            state.ReviewAssigned && set.Task is { } task && (task.CreatedByUserId == actor.UserId || task.AssignedToUserId == actor.UserId),
            ExamPackageWorkflow.Allowed("RENAMED", set.Status, state.Purpose, manager, reviewing, writable && actor.Pht),
            ExamPackageWorkflow.Allowed("REVIEW_ASSIGNED", set.Status, state.Purpose, manager, reviewing, writable && actor.Pht),
            reviewing, reviewing,
            ExamPackageWorkflow.Allowed("REVIEW_RETURNED", set.Status, state.Purpose, manager, reviewing, writable && actor.Pht),
            ExamPackageWorkflow.Allowed("FINALIZED", set.Status, state.Purpose, manager, reviewing, writable && actor.Pht),
            manager && set.Status == "DRAFT" && !state.ReviewAssigned && unused,
            writable && actor.Pht && state.Purpose == "exam" && set.Status == "APPROVED" && state.PracticeCopyId is null && !await UnfinishedExamAsync(id, ct),
            state.Cells, state.Rows.Select(row => new ExamPackageQuestion(row.RowId, row.DetailId, row.Snapshot)).ToArray(), history);
    }
}
