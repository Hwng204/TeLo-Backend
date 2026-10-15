using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed partial class ExamPackageService
{
    private sealed record Mutation(ExamSet Set, ExamMatrix Matrix, ExamPackageState State, int Version)
    {
        public ulong? OldQuestionId { get; set; }
        public ulong? NewQuestionId { get; set; }
    }
    private Task LockBranch(ulong branchId, CancellationToken ct) => db.SchoolBranches.FromSqlInterpolated(
        $"SELECT * FROM branch WHERE branch_id = {branchId} FOR UPDATE").ToArrayAsync(ct);

    private async Task<Mutation> LockAsync(QuestionActor actor, ulong id, int version, CancellationToken ct)
    {
        var branch = await (await ScopedAsync(actor, ct)).Where(x => x.Id == id).Select(x => (ulong?)x.ExamMatrix.BranchId).SingleOrDefaultAsync(ct) ?? throw Missing();
        Require((await UserAsync(actor, ct)).SchoolBranchId == branch, "Chỉ thao tác đề thuộc phân hiệu của bạn.", 403);
        await LockBranch(branch, ct);
        var set = (await db.ExamSets.FromSqlInterpolated($"SELECT * FROM exam_set WHERE exam_set_id = {id} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault() ?? throw Missing();
        if (set.TaskId is { } taskId) await db.LockTaskAsync(taskId, ct);
        await db.Entry(set).ReloadAsync(ct);
        if (!await (await ScopedAsync(actor, ct)).AnyAsync(x => x.Id == id, ct)) throw Missing();
        Require(set.TaskId.HasValue, "Đề chưa có nhiệm vụ lưu lịch sử. Cần liên kết nhiệm vụ trước khi chỉnh sửa.");
        var (state, actual) = await storage.ReadAsync(set, ct);
        if (state.Deleted) throw Missing();
        if (version < 0 || actual != version) throw new QuestionException("EXAM_PACKAGE_CONFLICT", "Đề đã thay đổi. Vui lòng tải lại trước khi tiếp tục.", 409);
        return new(set, await storage.MatrixAsync(set.ExamMatrixId, ct), state, actual);
    }

    private async Task<ExamPackageDetail> Mutate(QuestionActor actor, ulong id, int version, string action,
        Func<Mutation, Task<ExamPackageState>> apply, CancellationToken ct, string? comment = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var mutation = await LockAsync(actor, id, version, ct);
        var state = await apply(mutation);
        if (action == "RENAMED") comment = mutation.Set.Name;
        if (action == "REVIEW_ASSIGNED")
        {
            var reviewTask = db.WorkTasks.Local.Single(x => x.Id == mutation.Set.TaskId);
            comment = $"Người review #{mutation.Set.ApprovedByUserId}; hạn {reviewTask.DueAt:dd/MM/yyyy}";
        }
        await storage.WriteAsync(mutation.Set, state, mutation.Version, action, actor.UserId, ct, comment,
            mutation.OldQuestionId, mutation.NewQuestionId);
        await storage.SynchronizeAuthoringAsync(state, id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(actor, id, ct);
    }

    private async Task<ExamSet> NewSetAsync(QuestionActor actor, string name, ExamMatrix matrix, CancellationToken ct)
    {
        var task = new WorkTask { TaskType = ExamPackageStorage.InternalTaskType, Name = name, CreatedByUserId = actor.UserId,
            AssignedToUserId = actor.UserId, Status = "IN_PROGRESS", CreatedAt = Now };
        db.WorkTasks.Add(task); await db.SaveChangesAsync(ct);
        var set = new ExamSet { Name = name, ExamMatrixId = matrix.Id, TaskId = task.Id, Status = "DRAFT", DurationMinutes = 40 };
        db.ExamSets.Add(set); await db.SaveChangesAsync(ct);
        return set;
    }

    public async Task<ExamPackageDetail> GenerateAsync(QuestionActor actor, GenerateExamPackageRequest request, CancellationToken ct)
    {
        Require(actor.Pht, "Chỉ PHT được tạo đề tự động. Giáo viên hoặc tổ trưởng review sau khi được giao đề.", 403);
        var name = Name(request.Name); var user = await UserAsync(actor, ct);
        Require(user.SchoolBranchId is not null, "Tài khoản chưa được gán phân hiệu.", 403);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockBranch(user.SchoolBranchId!.Value, ct);
        var currentUser = await UserAsync(actor, ct);
        Require(currentUser.SchoolBranchId == user.SchoolBranchId, "Phân hiệu đã thay đổi. Vui lòng tải lại.", 403);
        var matrix = await storage.MatrixAsync(request.MatrixId, ct);
        Require(matrix.BranchId == user.SchoolBranchId, "Ma trận không thuộc phân hiệu của bạn.", 403);
        var selected = ExamPackageRules.Select(matrix, await Candidates(matrix).ToArrayAsync(ct));
        var set = await NewSetAsync(actor, name, matrix, ct);
        var nativeRows = selected.Select(selectedRow => new ExamSetQuestion { ExamSetId = set.Id, QuestionId = selectedRow.Question.Id }).ToArray();
        db.ExamSetQuestions.AddRange(nativeRows); await db.SaveChangesAsync(ct);
        var rows = nativeRows.Select((row, index) => new ExamStoredRow(row.Id, selected[index].DetailId, QuestionService.Item(selected[index].Question))).ToArray();
        var year = await db.AcademicYears.AsNoTracking().Where(x => x.Status == "ACTIVE").FirstOrDefaultAsync(ct);
        var state = new ExamPackageState(actor.UserId, Now, year?.Id, year?.Name ?? "", "exam", null, null, false,
            null, null, ExamPackageStorage.Cells(matrix), rows, []);
        await storage.WriteAsync(set, state, 0, "GENERATED", actor.UserId, ct);
        await tx.CommitAsync(ct);
        return await GetAsync(actor, set.Id, ct);
    }

    public Task<ExamPackageDetail> RenameAsync(QuestionActor actor, ulong id, RenameExamPackageRequest request, CancellationToken ct) =>
        Mutate(actor, id, request.Version, "RENAMED", context =>
        {
            Require(Manager(actor), "Chỉ PHT được cập nhật tên đề.", 403);
            Require(context.Set.Status == "DRAFT" && context.State.Purpose == "exam", "Chỉ sửa tên đề nháp.");
            context.Set.Name = Name(request.Name);
            return Task.FromResult(context.State);
        }, ct);

    public Task<ExamPackageDetail> AssignAsync(QuestionActor actor, ulong id, AssignExamReviewRequest request, CancellationToken ct) =>
        Mutate(actor, id, request.Version, "REVIEW_ASSIGNED", async context =>
        {
            var (set, matrix, state, _) = context;
            Require(Manager(actor), "Chỉ PHT được giao review đề này.", 403);
            Require(state.Purpose == "exam" && set.Status is "DRAFT" or "IN_REVIEW" or "REVIEWED", "Đề đã khóa, không thể giao review.");
            if (request.DueAt is null || DateOnly.FromDateTime(request.DueAt.Value) < TaskRules.Today(time))
                throw new QuestionException("VALIDATION_ERROR", "Chọn hạn review từ hôm nay trở đi.");
            var codes = TaskService.TeacherRoleCodes(configuration).Concat(roles.TeamLeadRoleCodes).ToArray();
            Require(request.ReviewerUserId != state.CreatorId && await db.Users.AnyAsync(x => x.Id == request.ReviewerUserId &&
                x.Status == "ACTIVE" && x.SchoolBranchId == matrix.BranchId && x.UserRoles.Any(grant => grant.Role.Status == "ACTIVE" && codes.Contains(grant.Role.Code)), ct),
                "Chọn giáo viên hoặc Tổ trưởng đang hoạt động cùng phân hiệu, khác người tạo đề.", 422);
            await ValidateRowsAsync(context, ct);
            var task = await db.WorkTasks.SingleAsync(x => x.Id == set.TaskId, ct);
            if (!state.ReviewAssigned && state.AuthoringTaskId is not null)
            {
                // One dedicated review task per set. Keep authoring-task links in
                // the journal, and transfer only this set's journal chunks.
                var title = "Review đề · " + set.Name;
                var newTask = new WorkTask { TaskType = "EXAM", Status = "ASSIGNED", Name = title[..Math.Min(255, title.Length)],
                    CreatedByUserId = actor.UserId, AssignedToUserId = request.ReviewerUserId, DueAt = request.DueAt, CreatedAt = Now };
                db.WorkTasks.Add(newTask); await db.SaveChangesAsync(ct);
                var stream = "\"stream\":\"" + ExamPackageStorage.Stream(id) + "\"";
                var history = await db.TaskHistories.AsNoTracking().Where(row => row.TaskId == task.Id && row.Content.StartsWith(TaskJournalCodec.Prefix) && row.Content.Contains(stream)).ToArrayAsync(ct);
                foreach (var row in history) db.TaskHistories.Add(new() { TaskId = newTask.Id, Content = row.Content, CreateBy = row.CreateBy, CreateAt = row.CreateAt });
                set.TaskId = newTask.Id; task = newTask;
                await db.SaveChangesAsync(ct);
            }
            var reviewTitle = "Review đề · " + set.Name;
            task.Name = reviewTitle[..Math.Min(255, reviewTitle.Length)];
            task.TaskType = "EXAM"; task.Status = "ASSIGNED"; task.AssignedToUserId = request.ReviewerUserId; task.DueAt = request.DueAt;
            task.UpdatedAt = Now; set.ApprovedByUserId = request.ReviewerUserId; set.Status = "IN_REVIEW";
            return state with { ReviewAssigned = true };
        }, ct);

    private async Task ValidateRowsAsync(Mutation context, CancellationToken ct)
    {
        var rows = await db.ExamSetQuestions.Where(x => x.ExamSetId == context.Set.Id).ToArrayAsync(ct);
        var ids = rows.Select(x => x.QuestionId).ToArray();
        var questions = await Candidates(context.Matrix).Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        Require(rows.Length == context.State.Rows.Count && questions.Length == rows.Length, "Đề có câu không còn hợp lệ.");
        ExamPackageRules.Validate(context.Matrix, rows.Select(row => (
            context.State.Rows.Single(x => x.RowId == row.Id && x.Snapshot.Id == row.QuestionId).DetailId,
            questions.Single(q => q.Id == row.QuestionId))).ToArray());
    }

    public async Task<ExamPackageDetail> ReplaceAsync(QuestionActor actor, ulong id, ulong rowId, ReplaceExamQuestionRequest request, CancellationToken ct)
    {
        return await Mutate(actor, id, request.Version, "QUESTION_REPLACED", async context =>
        {
            var (set, matrix, state, _) = context;
            Require(set.Status == "IN_REVIEW" && state.Purpose == "exam", "Chỉ thay câu khi đề đang review.");
            Require(Reviewing(actor, set, state), "Chỉ giáo viên hoặc tổ trưởng được giao review mới được thay câu.", 403);
            var stored = state.Rows.SingleOrDefault(x => x.RowId == rowId) ?? throw Missing();
            var cell = matrix.Details.Single(x => x.Id == stored.DetailId);
            var candidate = await Candidates(matrix).SingleOrDefaultAsync(x => x.Id == request.QuestionId, ct);
            Require(candidate is not null && ExamPackageRules.Matches(candidate, matrix, cell) &&
                state.Rows.All(x => x.RowId == rowId || x.Snapshot.Id != request.QuestionId), "Câu thay thế phải chưa có trong đề và đúng ô ma trận từ ngân hàng thi.", 422);
            var row = await db.ExamSetQuestions.SingleAsync(x => x.Id == rowId && x.ExamSetId == id, ct);
            context.OldQuestionId = row.QuestionId; context.NewQuestionId = request.QuestionId;
            row.QuestionId = request.QuestionId;
            var task = await db.WorkTasks.SingleAsync(x => x.Id == set.TaskId, ct); task.Status = "IN_PROGRESS"; task.UpdatedAt = Now;
            var replacement = new ExamStoredRow(rowId, cell.Id, QuestionService.Item(candidate!));
            return state with { Rows = state.Rows.Select(x => x.RowId == rowId ? replacement : x).ToArray() };
        }, ct);
    }

    public Task<ExamPackageDetail> SubmitAsync(QuestionActor actor, ulong id, ExamPackageCommand request, CancellationToken ct) =>
        Mutate(actor, id, request.Version, "REVIEW_SUBMITTED", async context =>
        {
            Require(context.Set.Status == "IN_REVIEW", "Đề không ở trạng thái đang review.");
            Require(Reviewing(actor, context.Set, context.State), "Chỉ người được giao review mới được gửi kết quả.", 403);
            await ValidateRowsAsync(context, ct); context.Set.Status = "REVIEWED";
            (await db.WorkTasks.SingleAsync(x => x.Id == context.Set.TaskId, ct)).Status = "PENDING_REVIEW";
            return context.State;
        }, ct, Comment(request.Comment));

    public Task<ExamPackageDetail> ReturnAsync(QuestionActor actor, ulong id, ExamPackageCommand request, CancellationToken ct) =>
        Mutate(actor, id, request.Version, "REVIEW_RETURNED", async context =>
        {
            Require(actor.Pht, "Chỉ PHT được trả lại kết quả review.", 403);
            Require(context.Set.Status == "REVIEWED", "Chỉ trả lại đề đã gửi review.");
            context.Set.Status = "IN_REVIEW";
            (await db.WorkTasks.SingleAsync(x => x.Id == context.Set.TaskId, ct)).Status = "NEEDS_REVISION";
            return context.State;
        }, ct, Comment(request.Comment, true));

    public Task<ExamPackageDetail> FinalizeAsync(QuestionActor actor, ulong id, ExamPackageCommand request, CancellationToken ct) =>
        Mutate(actor, id, request.Version, "FINALIZED", async context =>
        {
            Require(actor.Pht, "Chỉ PHT được chốt bản cuối.", 403);
            Require(context.Set.Status == "REVIEWED" && context.State.Purpose == "exam", "Cần kết quả review trước khi chốt đề.");
            await ValidateRowsAsync(context, ct);
            var ids = context.State.Rows.Select(x => x.Snapshot.Id).ToArray();
            var questions = await Candidates(context.Matrix).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            context.Set.Status = "APPROVED";
            (await db.WorkTasks.SingleAsync(x => x.Id == context.Set.TaskId, ct)).Status = "COMPLETED";
            return context.State with { FinalizedBy = actor.UserId, FinalizedAt = Now,
                Rows = context.State.Rows.Select(x => x with { Snapshot = QuestionService.Item(questions[x.Snapshot.Id]) }).ToArray() };
        }, ct);

    public async Task<ExamPackageDetail> PracticeAsync(QuestionActor actor, ulong id, ExamPackageCommand request, CancellationToken ct)
    {
        ulong copyId = 0;
        await Mutate(actor, id, request.Version, "PRACTICE_COPIED", async context =>
        {
            Require(actor.Pht, "Chỉ PHT được chuyển đề sang ôn tập.", 403);
            Require(context.Set.Status == "APPROVED" && context.State.Purpose == "exam" && context.State.PracticeCopyId is null, "Chỉ sao chép một lần từ đề thi đã chốt.");
            Require(!await UnfinishedExamAsync(id, ct), "Đề đang được kỳ thi chưa kết thúc sử dụng, không được công bố ôn tập.");
            var name = "Ôn tập · " + context.Set.Name;
            var copy = await NewSetAsync(actor, name[..Math.Min(255, name.Length)], context.Matrix, ct);
            copy.DurationMinutes = context.Set.DurationMinutes;
            var nativeRows = context.State.Rows.Select(source => new ExamSetQuestion { ExamSetId = copy.Id, QuestionId = source.Snapshot.Id }).ToArray();
            db.ExamSetQuestions.AddRange(nativeRows); await db.SaveChangesAsync(ct);
            var rows = context.State.Rows.Select((source, index) => source with { RowId = nativeRows[index].Id }).ToArray();
            copy.Status = "APPROVED";
            var state = context.State with { CreatorId = actor.UserId, CreatedAt = Now, Purpose = "practice", SourceId = id,
                AuthoringTaskId = null, ReviewAssigned = false, FinalizedBy = actor.UserId, FinalizedAt = Now,
                Rows = rows, History = [], PracticeCopyId = null };
            await storage.WriteAsync(copy, state, 0, "PRACTICE_CREATED", actor.UserId, ct, $"Từ đề #{id}");
            copyId = copy.Id;
            return context.State with { PracticeCopyId = copyId };
        }, ct);
        return await GetAsync(actor, copyId, ct);
    }

    public async Task<bool> DeleteAsync(QuestionActor actor, ulong id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var context = await LockAsync(actor, id, version, ct);
        Require(Manager(actor), "Chỉ PHT được xóa đề nháp.", 403);
        Require(context.Set.Status == "DRAFT" && context.State.Purpose == "exam" && !context.State.ReviewAssigned &&
            !await ReferencedAsync(id, ct) && !await db.ExamVariants.AnyAsync(x => x.ExamSetId == id, ct) && context.State.PracticeCopyId is null,
            "Chỉ xóa đề nháp chưa giao review hoặc sử dụng.");
        db.ExamSetQuestions.RemoveRange(await db.ExamSetQuestions.Where(x => x.ExamSetId == id).ToArrayAsync(ct));
        if (context.State.AuthoringTaskId is not null)
            await storage.WriteAsync(context.Set, context.State with { Deleted = true, Rows = [] }, version, "DELETED", actor.UserId, ct);
        else db.ExamSets.Remove(context.Set);
        await storage.SynchronizeAuthoringAsync(context.State, id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return true;
    }
}
