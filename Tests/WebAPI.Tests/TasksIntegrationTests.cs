using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.Json;
using Application.DTOs;
using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

// Toàn bộ luồng nhiệm vụ qua HTTP thật (JWT đăng nhập, policy, TaskService, MySQL) trên DB tạm riêng.
[Collection(nameof(TasksIntegrationTests))]
public sealed class TasksIntegrationTests
{
    private const string QuestionUrl = "/api/tasks/question";
    private const string ExamUrl = "/api/tasks/exam";

    [IdentityMySqlFact]
    public async Task QuestionTask_TeamLeadAssignsTeachersAndOnlyInvolvedPeopleSeeIt()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var tl = await f.LoginAsync("tl");
        using var pht = await f.LoginAsync("pht");
        using var gv1 = await f.LoginAsync("gv1");
        using var gv2 = await f.LoginAsync("gv2");
        using var gv3 = await f.LoginAsync("gv3");
        var (a1, a2, b) = (f.LessonA1.Id, f.LessonA2.Id, f.LessonB.Id);

        var options = Data(await Send(tl, HttpMethod.Get, "/api/tasks/options"));
        Assert.Equal(new[] { "gv1", "gv2", "gv3" }, Names(options.GetProperty("teachers"), "username").Order());
        Assert.Empty(options.GetProperty("teamLeads").EnumerateArray());

        Assert.Equal("assignedToUserId", Invalid(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gvB"), [(a1, 1, 0, 0)]))));
        Assert.Equal("assignedToUserId", Invalid(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("tl2"), [(a1, 1, 0, 0)]))));
        Assert.Equal("lessons", Invalid(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(b, 1, 0, 0)]))));
        Assert.Equal("lessons", Invalid(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(a1, 0, 0, 0)]))));
        Assert.Equal("dueAt", Invalid(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(a1, 1, 0, 0)], due: -1))));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(a1, 1, 0, 0)]))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv1, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(a1, 1, 0, 0)]))).Status);
        Assert.False(await f.Db.WorkTasks.AnyAsync());

        // Client cũ còn gửi reviewerUserId, reviewDueAt: vẫn tạo được, người kiểm duyệt là Tổ trưởng giao việc.
        var created = Data(await Send(tl, HttpMethod.Post, QuestionUrl, new
        {
            name = "Biên soạn câu hỏi · Số và phép tính", assignedToUserId = f.Id("gv1"), dueAt = Day(5),
            reviewerUserId = f.Id("gv2"), reviewDueAt = Day(8),
            lessons = new[] { new { lessonId = a1, knowledge = 8, understanding = 6, application = 4 }, new { lessonId = a2, knowledge = 6, understanding = 4, application = 2 } }
        }));
        var id = created.GetProperty("id").GetUInt64();
        Assert.Equal(("QUESTION", "Câu hỏi", "ASSIGNED", "Đã giao"), (Text(created, "taskType"), Text(created, "taskTypeLabel"), Text(created, "status"), Text(created, "statusLabel")));
        Assert.Equal(("Lê Thu Hà", "Giáo viên"), Person(created, "performer"));
        Assert.Equal(("Nguyễn Thị Lan", "Tổ trưởng"), Person(created, "reviewer"));
        Assert.Equal(("Nguyễn Thị Lan", "Tổ trưởng"), Person(created, "createdBy"));
        Assert.False(created.TryGetProperty("reviewDueAt", out _));
        Assert.Equal(new[] { (a1, 18u, 8u), (a2, 12u, 6u) }, created.GetProperty("lessons").EnumerateArray()
            .Select(lesson => (lesson.GetProperty("lessonId").GetUInt64(), lesson.GetProperty("total").GetUInt32(), lesson.GetProperty("knowledge").GetUInt32())));
        Assert.Equal("Phân số", Text(created.GetProperty("lessons")[0], "lessonTitle"));

        var mine = Assert.Single(await ListAsync(tl), item => Id(item) == id);
        Assert.True(mine.GetProperty("canEdit").GetBoolean() && mine.GetProperty("canDelete").GetBoolean());
        Assert.Equal("Nguyễn Thị Lan", Person(mine, "reviewer").Name);
        Assert.False(Assert.Single(await ListAsync(gv1), item => Id(item) == id).GetProperty("canEdit").GetBoolean());
        Assert.DoesNotContain(await ListAsync(gv2), item => Id(item) == id);
        Assert.DoesNotContain(await ListAsync(gv3), item => Id(item) == id);
        Assert.DoesNotContain(await ListAsync(pht), item => Id(item) == id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv2, HttpMethod.Get, $"/api/tasks/{id}")).Status);
        Assert.Equal(id, Id(Data(await Send(gv1, HttpMethod.Get, $"/api/tasks/{id}"))));

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv1, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv1"), [(a1, 1, 0, 0)]))).Status);
        var updated = Data(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv1"), [(a2, 1, 0, 0)], 6)));
        Assert.Equal(a2, Assert.Single(updated.GetProperty("lessons").EnumerateArray()).GetProperty("lessonId").GetUInt64());
        Assert.Equal(("Nguyễn Thị Lan", "Tổ trưởng"), Person(updated, "reviewer"));
        Assert.DoesNotContain(await ListAsync(gv3), item => Id(item) == id);

        // Giáo viên đã soạn câu hỏi: không đổi được bài, số câu, người thực hiện; vẫn sửa được hạn.
        await f.AddQuestionAsync(id, f.Id("gv1"));
        Assert.Equal("TASK_STARTED", Code(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv1"), [(a2, 2, 0, 0)], 6)), HttpStatusCode.Conflict));
        Assert.Equal("TASK_STARTED", Code(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv2"), [(a2, 1, 0, 0)], 6)), HttpStatusCode.Conflict));
        var extended = Data(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv1"), [(a2, 1, 0, 0)], 10)));
        Assert.Equal(1, extended.GetProperty("lessons")[0].GetProperty("written").GetInt32());
        Assert.True(extended.GetProperty("isStarted").GetBoolean());
        Assert.Equal("TASK_STARTED", Code(await Send(tl, HttpMethod.Delete, $"/api/tasks/{id}"), HttpStatusCode.Conflict));
        Assert.False(Assert.Single(await ListAsync(tl), item => Id(item) == id).GetProperty("canDelete").GetBoolean());

        await f.Db.Questions.ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv1, HttpMethod.Delete, $"/api/tasks/{id}")).Status);
        var deleted = await Send(tl, HttpMethod.Delete, $"/api/tasks/{id}");
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal("Đã xóa nhiệm vụ \"Biên soạn câu hỏi · Số và phép tính\".", Text(deleted.Body, "message"));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(tl, HttpMethod.Get, $"/api/tasks/{id}")).Status);
        Assert.False(await f.Db.QuestionTasks.AnyAsync(lesson => lesson.TaskId == id));
    }

    [IdentityMySqlFact]
    public async Task ExamTask_BlankPapersAreNoLongerAssigned_ButAnExistingExamTaskIsStillEditable()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        using var tl2 = await f.LoginAsync("tl2");
        using var gv1 = await f.LoginAsync("gv1");

        // Không còn chọn kỳ thi: chỉ chọn ma trận đã duyệt của phân hiệu (kỳ thi chọn đề sau).
        var options = Data(await Send(pht, HttpMethod.Get, "/api/tasks/options"));
        Assert.Equal(new[] { "tl", "tl2" }, Names(options.GetProperty("teamLeads"), "username").Order());
        Assert.False(options.TryGetProperty("exams", out _));
        var option = Assert.Single(options.GetProperty("matrices").EnumerateArray());
        // Matrices have no academic year any more (D10): the year name is empty.
        Assert.Equal((f.ApprovedA.Id, "Toán 5 · Cuối học kỳ I", "Toán thử", "Khối thử", ""),
            (Id(option), Text(option, "name"), Text(option, "subjectName"), Text(option, "gradeLevelName"), Text(option, "academicYearName")));
        Assert.Empty(options.GetProperty("teachers").EnumerateArray());

        // PHT now generates a full package in Kho đề and assigns its review; this route never makes blank papers.
        Assert.Equal("EXAM_TASK_FLOW_CHANGED", Code(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"))), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv1, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))).Status);
        Assert.False(await f.Db.WorkTasks.AnyAsync());

        // An exam task assigned before the new flow (blank papers Đề 1, Đề 2) is still edited through PUT.
        var legacy = new WorkTask
        {
            TaskType = "EXAM", Status = "ASSIGNED", Name = "Tạo bộ đề Toán 5 · Cuối học kỳ I",
            CreatedByUserId = f.Id("pht"), AssignedToUserId = f.Id("tl"), DueAt = DateTime.Today.AddDays(7), CreatedAt = DateTime.UtcNow
        };
        f.Db.WorkTasks.Add(legacy);
        foreach (var name in new[] { "Đề 1", "Đề 2" })
            f.Db.ExamSets.Add(new ExamSet { Task = legacy, Name = name, Status = ExamSetStatusCodes.Draft, ExamMatrixId = f.ApprovedA.Id, DurationMinutes = 40 });
        await f.Db.SaveChangesAsync();
        var id = legacy.Id;

        var created = Data(await Send(pht, HttpMethod.Get, $"/api/tasks/{id}"));
        var exam = created.GetProperty("exam");
        Assert.Equal((f.ApprovedA.Id, "Toán 5 · Cuối học kỳ I", "Toán thử", "Khối thử", ""),
            (exam.GetProperty("matrixId").GetUInt64(), Text(exam, "matrixName"), Text(exam, "subjectName"), Text(exam, "gradeLevelName"), Text(exam, "academicYearName")));
        Assert.False(exam.TryGetProperty("examId", out _));
        Assert.False(exam.TryGetProperty("examName", out _));
        Assert.Equal(new[] { "Đề 1", "Đề 2" }, Names(exam.GetProperty("papers"), "name"));
        Assert.All(exam.GetProperty("papers").EnumerateArray(), paper => Assert.Equal("Chưa tạo", Text(paper, "statusLabel")));
        Assert.Equal(f.ContextA, created.GetProperty("scope").GetProperty("academicContextId").GetUInt64());
        Assert.Equal(("Trần Thị Mai", "Phó Hiệu trưởng"), Person(created, "reviewer"));
        Assert.Equal(("Nguyễn Thị Lan", "Tổ trưởng"), Person(created, "performer"));

        var assigned = Assert.Single(await ListAsync(tl), item => Id(item) == id);
        Assert.False(assigned.GetProperty("canEdit").GetBoolean());
        Assert.Equal("Đề thi", Text(assigned, "taskTypeLabel"));
        Assert.DoesNotContain(await ListAsync(gv1), item => Id(item) == id);
        Assert.All(await ListAsync(pht, "?taskType=EXAM"), item => Assert.Equal("EXAM", Text(item, "taskType")));
        Assert.DoesNotContain(await ListAsync(pht, "?taskType=QUESTION"), item => Id(item) == id);

        // Chưa bắt đầu: đổi số đề thì dựng lại Đề 1 … Đề N.
        var updated = Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 9, 3)));
        Assert.Equal(new[] { "Đề 1", "Đề 2", "Đề 3" }, Names(updated.GetProperty("exam").GetProperty("papers"), "name"));
        var shrunk = Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 9, 1)));
        Assert.Equal(new[] { "Đề 1" }, Names(shrunk.GetProperty("exam").GetProperty("papers"), "name"));
        Assert.Equal(1, await f.Db.ExamSets.CountAsync(set => set.TaskId == id));
        Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 9, 3)));
        Assert.DoesNotContain(await ListAsync(tl), item => Id(item) == id);
        Assert.Contains(await ListAsync(tl2), item => Id(item) == id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl2, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2")))).Status);

        // Ma trận nguồn đã lưu trữ: giữ nguyên thì vẫn sửa được hạn.
        await f.Db.ExamMatrices.Where(matrix => matrix.Id == f.ApprovedA.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(matrix => matrix.Status, MatrixStatusCodes.Archived));
        Assert.Equal(id, Id(Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 12, 3)))));

        // Đã bắt đầu: số đề bị khoá như nội dung chuyên môn khác, vẫn sửa được hạn.
        await f.SetStatusAsync(id, "IN_PROGRESS");
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 12, 4)), HttpStatusCode.Conflict));
        var extended = Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 14, 3)));
        Assert.Equal(new[] { "Đề 1", "Đề 2", "Đề 3" }, Names(extended.GetProperty("exam").GetProperty("papers"), "name"));

        await f.SetStatusAsync(id, "COMPLETED");
        Assert.Equal("TASK_COMPLETED", Code(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"))), HttpStatusCode.Conflict));
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Delete, $"/api/tasks/{id}"), HttpStatusCode.Conflict));
    }

    [IdentityMySqlFact]
    public async Task MatrixTask_VicePrincipalEditsScopeOnlyUntilTheTeamLeadStarts()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl2 = await f.LoginAsync("tl2");
        using var admin = await f.LoginAsync("admin");
        // GB-58 email on assignment is locked with the email module (DB v2), so no notification is checked here.
        object Matrix(string assignee, ulong contextId, int due, string name = "Ma trận Toán 5 - Cuối học kỳ I") =>
            new { assignedToUserId = f.Id(assignee), academicContextId = contextId, semesterId = RemovedSemesterId, dueAt = Day(due), name, description = "20 câu trắc nghiệm" };

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(pht, HttpMethod.Post, "/api/matrix-tasks", Matrix("tl", f.ContextA, -1))).Status);
        var created = await Send(pht, HttpMethod.Post, "/api/matrix-tasks", Matrix("tl", f.ContextA, 3));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = Id(created.Body);

        var item = Assert.Single(await ListAsync(pht), row => Id(row) == id);
        Assert.Equal(("Ma trận", "Nguyễn Thị Lan", "Trần Thị Mai"), (Text(item, "taskTypeLabel"), Person(item, "performer").Name, Person(item, "reviewer").Name));
        Assert.True(item.GetProperty("canEdit").GetBoolean());

        var url = $"/api/tasks/matrix/{id}";
        Assert.Equal("assignedToUserId", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("gv1", f.ContextA, 3))));
        Assert.Equal("academicContextId", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextB, 3))));
        Assert.Equal("dueAt", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextA, -2))));

        var updated = Data(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA, 4, "Ma trận Toán 5 - Giữa học kỳ I")));
        Data(await Send(pht, HttpMethod.Put, url, new
        {
            assignedToUserId = f.Id("tl2"), academicContextId = f.ContextA, semesterId = RemovedSemesterId, dueAt = Day(4),
            name = "Ma trận Toán 5 - Giữa học kỳ I", description = "Chỉ đổi yêu cầu"
        }));
        Assert.Equal(("Ma trận Toán 5 - Giữa học kỳ I", "Đỗ Văn Tổ"), (Text(updated, "name"), Person(updated, "performer").Name));
        // Tasks have no year or semester any more (D10): the scope is the matrix's subject and grade.
        var scope = updated.GetProperty("scope");
        Assert.Equal(("Toán thử", "Khối thử", "", JsonValueKind.Null),
            (Text(scope, "subjectName"), Text(scope, "gradeLevelName"), Text(scope, "academicYearName"), scope.GetProperty("semesterName").ValueKind));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl2, HttpMethod.Put, url, Matrix("tl2", f.ContextA, 4))).Status);

        // Tổ trưởng lưu nháp ma trận: không đổi được người nhận và phạm vi; bản nháp vẫn riêng của Tổ trưởng (GB-47).
        var draftId = await f.AddTaskMatrixAsync(id);
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextA, 4)), HttpStatusCode.Conflict));
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA2, 4)), HttpStatusCode.Conflict));
        var renamed = Data(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA, 6, "Ma trận đổi tên")));
        Assert.Equal("Ma trận đổi tên", Text(renamed, "name"));
        var hidden = renamed.GetProperty("matrix");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "DRAFT"), (hidden.GetProperty("id").ValueKind, hidden.GetProperty("name").ValueKind, Text(hidden, "status")));
        Assert.Equal(draftId, Data(await Send(tl2, HttpMethod.Get, $"/api/tasks/{id}")).GetProperty("matrix").GetProperty("id").GetUInt64());
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Delete, $"/api/tasks/{id}"), HttpStatusCode.Conflict));
    }

    [IdentityMySqlFact]
    public async Task TaskList_IsLimitedToBranchAccountsAndFiltersByContentStatusAndDeadline()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var tl = await f.LoginAsync("tl");
        using var gv1 = await f.LoginAsync("gv1");
        using var gvB = await f.LoginAsync("gvB");
        using var noBranch = await f.LoginAsync("nobranch");
        using var student = await f.LoginAsync("student");
        using var anonymous = f.CreateClient();

        Assert.Equal("BRANCH_REQUIRED", Code(await Send(noBranch, HttpMethod.Get, "/api/tasks"), HttpStatusCode.Forbidden));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(student, HttpMethod.Get, "/api/tasks")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, HttpMethod.Get, "/api/tasks")).Status);

        var fractions = Id(Data(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(f.LessonA1.Id, 2, 0, 0)], name: "Biên soạn câu hỏi · Phân số"))));
        var decimals = Id(Data(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv2"), [(f.LessonA2.Id, 0, 3, 0)], name: "Biên soạn câu hỏi · Số thập phân"))));

        Assert.Equal(new[] { decimals, fractions }, (await ListAsync(tl)).Select(Id));
        Assert.Equal(new[] { fractions }, (await ListAsync(gv1)).Select(Id));
        Assert.Empty(await ListAsync(gvB));

        // Giáo viên nghỉ: để nguyên thì vẫn sửa được hạn, nhưng không giao thêm việc mới cho người đó.
        await f.Db.Users.Where(user => user.Id == f.Id("gv2")).ExecuteUpdateAsync(set => set.SetProperty(user => user.Status, "INACTIVE"));
        Assert.Equal(decimals, Id(Data(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{decimals}",
            QuestionBody(f.Id("gv2"), [(f.LessonA2.Id, 0, 3, 0)], 6, "Biên soạn câu hỏi · Số thập phân")))));
        Assert.Equal("assignedToUserId", Invalid(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{fractions}",
            QuestionBody(f.Id("gv2"), [(f.LessonA1.Id, 2, 0, 0)], name: "Biên soạn câu hỏi · Phân số"))));
        await f.Db.Users.Where(user => user.Id == f.Id("gv2")).ExecuteUpdateAsync(set => set.SetProperty(user => user.Status, "ACTIVE"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gvB, HttpMethod.Get, $"/api/tasks/{fractions}")).Status);
        Assert.Equal(new[] { decimals }, (await ListAsync(tl, "?keyword=thập phân")).Select(Id));
        Assert.Empty(await ListAsync(tl, "?taskType=MATRIX"));

        await f.Db.WorkTasks.Where(task => task.Id == fractions).ExecuteUpdateAsync(set => set.SetProperty(task => task.DueAt, DateTime.Today.AddDays(-2)));
        var overdue = Assert.Single(await ListAsync(tl, "?status=OVERDUE"));
        Assert.Equal(fractions, Id(overdue));
        Assert.True(overdue.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(2, (await ListAsync(tl, "?status=ASSIGNED")).Length);

        // Quá hạn chỉ theo hạn hoàn thành, khi người thực hiện còn việc (kể cả cần chỉnh sửa): chờ kiểm duyệt, hoàn thành thì không.
        await f.Db.WorkTasks.Where(task => task.Id == decimals).ExecuteUpdateAsync(set => set
            .SetProperty(task => task.Status, "IN_PROGRESS").SetProperty(task => task.DueAt, DateTime.Today.AddDays(-1)));
        Assert.Equal(new[] { decimals, fractions }, (await ListAsync(tl, "?status=OVERDUE")).Select(Id));
        await f.SetStatusAsync(decimals, "PENDING_REVIEW");
        Assert.Equal(new[] { fractions }, (await ListAsync(tl, "?status=OVERDUE")).Select(Id));
        var pending = Assert.Single(await ListAsync(tl, "?status=PENDING_REVIEW"));
        Assert.Equal("Chờ kiểm duyệt", Text(pending, "statusLabel"));
        Assert.False(pending.GetProperty("isOverdue").GetBoolean());
        await f.SetStatusAsync(fractions, "NEEDS_REVISION");
        var revising = Assert.Single(await ListAsync(tl, "?status=OVERDUE"));
        Assert.Equal(("Cần chỉnh sửa", true), (Text(revising, "statusLabel"), revising.GetProperty("isOverdue").GetBoolean()));
        await f.SetStatusAsync(fractions, "COMPLETED");
        Assert.Empty(await ListAsync(tl, "?status=OVERDUE"));
        // "Đã nộp" đã gộp vào "Chờ kiểm duyệt".
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(tl, HttpMethod.Get, "/api/tasks?status=SUBMITTED")).Status);

        var page = Data(await Send(tl, HttpMethod.Get, "/api/tasks?page=2&pageSize=1"));
        Assert.Equal((2, 1, 2), (page.GetProperty("totalCount").GetInt32(), page.GetProperty("items").GetArrayLength(), page.GetProperty("totalPages").GetInt32()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(gv1, HttpMethod.Get, "/api/tasks?status=DONE")).Status);
    }

    [IdentityMySqlFact]
    public async Task LegacyMatrixTask_OnlyParticipantsRead()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var pht2 = await f.LoginAsync("pht2");
        using var principal = await f.LoginAsync("principal");
        using var tl = await f.LoginAsync("tl");
        var created = await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = Id(created.Body);

        foreach (var actor in new[] { pht2, principal })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(actor, HttpMethod.Get, $"/api/matrix-tasks/{id}")).Status);
            Assert.Empty(await ListAsync(actor));
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Get, $"/api/matrix-tasks/{id}")).Status);
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_UnrelatedVicePrincipalCannotCreateTheAssignedMatrix()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var pht2 = await f.LoginAsync("pht2");
        var id = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht2, HttpMethod.Post, "/api/matrices", MatrixBody(f, id))).Status);
        // The task's matrix exists from creation and stays empty.
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == id && matrix.Details.Any()));
    }

    // Audit #1: the assigning PHT reviews the matrix; only the assigned Team Lead drafts it.
    [IdentityMySqlFact]
    public async Task TaskMatrix_AssigningVicePrincipalCannotDraftIt()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId))).Status);
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == taskId && matrix.Details.Any()));
        Assert.True(await f.Db.WorkTasks.AnyAsync(task => task.Id == taskId && task.Status == "ASSIGNED"));
    }

    // Audit #5: the save that loaded the rows before the other one committed gets 409, not 500; audit #17: a save touches the task.
    [IdentityMySqlFact]
    public async Task TaskMatrix_ConcurrentSavesGiveOneConflictAndTouchTheTask()
    {
        var barrier = new ReferenceReadBarrier("matrix");
        await using var f = await TaskFixture.CreateAsync(barrier);
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        var id = Id((await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId))).Body);
        var old = new DateTime(2000, 1, 1);
        await f.Db.WorkTasks.Where(task => task.Id == taskId).ExecuteUpdateAsync(set => set.SetProperty(task => task.UpdatedAt, old));
        object Body(string level) => new
        {
            name = "Ma trận của nhiệm vụ", academicContextId = f.ContextA, taskId, totalScore = 10,
            details = new[] { new { lessonId = f.LessonA1.Id, cognitiveLevel = level, questionCount = 2, percentage = 100 } }
        };

        barrier.Arm();
        var late = Send(tl, HttpMethod.Put, $"/api/matrices/{id}", Body("VAN_DUNG"));
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Put, $"/api/matrices/{id}", Body("THONG_HIEU"))).Status);
        }
        finally { barrier.Release.TrySetResult(); }

        Assert.Equal(HttpStatusCode.Conflict, (await late.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        var levels = await f.Db.MatrixDetails.Where(detail => detail.ExamMatrixId == id).Select(detail => detail.CognitiveLevel).ToListAsync();
        Assert.Equal("THONG_HIEU", Assert.Single(levels));
        var stored = await f.Db.WorkTasks.AsNoTracking().SingleAsync(item => item.Id == taskId);
        Assert.Equal("IN_PROGRESS", stored.Status);
        Assert.True(stored.UpdatedAt > old);
    }

    // Audit #6: a draft restarted after deleting the returned one does not show the old rejection.
    [IdentityMySqlFact]
    public async Task TaskMatrix_RestartedDraftDoesNotShowTheOldRejection()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        var id = Id((await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId))).Body);
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        var rejected = await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/reject", new { comment = "Sai tỷ lệ chương 2" });
        Assert.Equal("Sai tỷ lệ chương 2", Text(rejected.Body, "rejectComment"));

        Assert.Equal(HttpStatusCode.NoContent, (await Send(tl, HttpMethod.Delete, $"/api/matrices/{id}")).Status);
        var restarted = await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId));
        Assert.Equal(HttpStatusCode.Created, restarted.Status);
        Assert.Equal(JsonValueKind.Null, restarted.Body.GetProperty("rejectComment").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Send(tl, HttpMethod.Get, $"/api/matrices/{id}")).Body.GetProperty("rejectComment").ValueKind);
        var matrix = Data(await Send(tl, HttpMethod.Get, $"/api/tasks/{taskId}")).GetProperty("matrix");
        Assert.Equal(JsonValueKind.Null, matrix.GetProperty("rejectComment").ValueKind);
    }

    // Audit #12 (and #10): an archived lesson/chapter blocks submit and confirm, yet still shows its name.
    [IdentityMySqlFact]
    public async Task Matrix_ArchivedLessonsBlockSubmitAndConfirmButKeepTheirNames()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        var taskMatrix = Id((await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId))).Body);
        var direct = await Send(pht, HttpMethod.Post, "/api/matrices", new
        {
            name = "Ma trận tự tạo", academicContextId = f.ContextA, totalScore = 10,
            details = new[] { new { lessonId = f.LessonA2.Id, cognitiveLevel = "NHAN_BIET", questionCount = 2, percentage = 100 } }
        });
        Assert.Equal(HttpStatusCode.Created, direct.Status);
        var directId = Id(direct.Body);

        await f.Db.Lessons.Where(lesson => lesson.Id == f.LessonA1.Id).ExecuteUpdateAsync(set => set.SetProperty(lesson => lesson.Status, "INACTIVE"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(tl, HttpMethod.Post, $"/api/matrices/{taskMatrix}/submit")).Status);
        var detail = (await Send(tl, HttpMethod.Get, $"/api/matrices/{taskMatrix}")).Body.GetProperty("details")[0];
        Assert.Equal(("Phân số", "Số và phép tính"), (Text(detail, "lessonTitle"), Text(detail, "chapterTitle")));

        await f.Db.Chapters.Where(chapter => chapter.Id == f.LessonA2.ChapterId).ExecuteUpdateAsync(set => set.SetProperty(chapter => chapter.Status, "INACTIVE"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(pht, HttpMethod.Post, $"/api/matrices/{directId}/confirm")).Status);
        Assert.Equal(2, await f.Db.ExamMatrices.CountAsync(matrix => (matrix.Id == taskMatrix || matrix.Id == directId) && matrix.Status == "DRAFT"));
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_QuestionTotalOverflowReturnsValidationAndDoesNotPersist()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        var response = await Send(tl, HttpMethod.Post, "/api/matrices", new
        {
            name = "Tổng số câu quá lớn", academicContextId = f.ContextA, semesterId = RemovedSemesterId, taskId, totalScore = 10,
            details = new[]
            {
                new { lessonId = f.LessonA1.Id, cognitiveLevel = "NHAN_BIET", questionCount = uint.MaxValue, percentage = 50 },
                new { lessonId = f.LessonA1.Id, cognitiveLevel = "THONG_HIEU", questionCount = 1u, percentage = 50 }
            }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == taskId && matrix.Details.Any()));
    }

    // A task has no academic year any more (D10): its scope is its matrix's.
    [IdentityMySqlFact]
    public async Task MatrixTask_ScopeComesFromItsMatrixAndAllowsDeadlineEdits()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        Assert.True(MatrixScope.TryParse(f.ContextA, out var expected));

        var scope = Data(await Send(pht, HttpMethod.Get, $"/api/tasks/{taskId}")).GetProperty("scope");
        Assert.Equal(f.ContextA, scope.GetProperty("academicContextId").GetUInt64());
        Assert.Equal(JsonValueKind.Null, scope.GetProperty("academicYearId").ValueKind);
        Assert.Equal(expected.SubjectId, scope.GetProperty("subjectId").GetUInt64());
        Assert.Equal(expected.GradeLevelId, scope.GetProperty("gradeLevelId").GetUInt64());
        var updated = Data(await Send(pht, HttpMethod.Put, $"/api/tasks/matrix/{taskId}", new
        {
            assignedToUserId = f.Id("tl"), academicContextId = f.ContextA, semesterId = RemovedSemesterId,
            dueAt = Day(10), name = "Gia hạn nhiệm vụ", description = "Giữ nguyên phạm vi"
        }));
        Assert.Equal(f.ContextA, updated.GetProperty("scope").GetProperty("academicContextId").GetUInt64());
        Assert.Equal(Day(10), Text(updated, "dueAt"));
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_OnlyItsTaskCreatorCanApproveOrReject()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var pht2 = await f.LoginAsync("pht2");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        var matrix = await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId));
        Assert.Equal(HttpStatusCode.Created, matrix.Status);
        var id = Id(matrix.Body);
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);

        var unrelatedView = await Send(pht2, HttpMethod.Get, $"/api/matrices/{id}");
        Assert.Equal(HttpStatusCode.OK, unrelatedView.Status);
        var actions = unrelatedView.Body.GetProperty("allowedActions").EnumerateArray().Select(action => action.GetString()).ToArray();
        Assert.DoesNotContain("Update", actions);
        Assert.DoesNotContain("Approve", actions);
        Assert.DoesNotContain("Reject", actions);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht2, HttpMethod.Put, $"/api/matrices/{id}", MatrixBody(f, taskId))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht2, HttpMethod.Post, $"/api/matrices/{id}/reject", new { comment = "Không phải người giao" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht2, HttpMethod.Post, $"/api/matrices/{id}/approve")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/approve")).Status);
        Assert.Equal("COMPLETED", Text(Data(await Send(pht, HttpMethod.Get, $"/api/tasks/{taskId}")), "status"));
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_ReassignmentBeforeCreationLocksCannotUseThePreviousAssignee()
    {
        var barrier = new ReferenceReadBarrier("task");
        await using var f = await TaskFixture.CreateAsync(barrier);
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        barrier.Arm();
        var creation = Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId));
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Data(await Send(pht, HttpMethod.Put, $"/api/tasks/matrix/{taskId}", MatrixTaskBody(f, "tl2")));
        }
        finally { barrier.Release.TrySetResult(); }

        Assert.Equal(HttpStatusCode.Forbidden, (await creation.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == taskId && matrix.Details.Any()));
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_TaskStatusFollowsTheMatrixDraftReviewAndApproval()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        async Task<JsonElement> Detail() => Data(await Send(pht, HttpMethod.Get, $"/api/tasks/{taskId}"));
        async Task<(string, string)> Status() { var detail = await Detail(); return (Text(detail, "status"), Text(detail, "statusLabel")); }
        Assert.Equal(("ASSIGNED", "Đã giao"), await Status());

        // Lưu nháp → Đang thực hiện; xoá bản nháp → Đã giao.
        var draft = await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId));
        Assert.Equal(HttpStatusCode.Created, draft.Status);
        Assert.Equal(("IN_PROGRESS", "Đang thực hiện"), await Status());
        Assert.Equal(HttpStatusCode.NoContent, (await Send(tl, HttpMethod.Delete, $"/api/matrices/{Id(draft.Body)}")).Status);
        Assert.Equal(("ASSIGNED", "Đã giao"), await Status());

        var matrix = await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId));
        Assert.Equal(HttpStatusCode.Created, matrix.Status);
        var id = Id(matrix.Body);
        Assert.Equal("IN_PROGRESS", (await Status()).Item1);

        // Nộp → Chờ kiểm duyệt; từ chối → Cần chỉnh sửa, lại tính quá hạn vì Tổ trưởng còn việc phải làm.
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        Assert.Equal(("PENDING_REVIEW", "Chờ kiểm duyệt"), await Status());
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/reject", new { comment = "Thêm câu vận dụng" })).Status);
        Assert.Equal(("NEEDS_REVISION", "Cần chỉnh sửa"), await Status());
        await f.Db.WorkTasks.Where(task => task.Id == taskId)
            .ExecuteUpdateAsync(set => set.SetProperty(task => task.DueAt, DateTime.Today.AddDays(-1)));
        Assert.True((await Detail()).GetProperty("isOverdue").GetBoolean());
        Assert.Equal(taskId, Id(Assert.Single(await ListAsync(tl, "?status=OVERDUE"))));
        Assert.Equal(taskId, Id(Assert.Single(await ListAsync(pht, "?status=NEEDS_REVISION"))));

        // Nộp lại → Chờ kiểm duyệt (hết quá hạn); duyệt → Hoàn thành.
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        Assert.Equal(("PENDING_REVIEW", "Chờ kiểm duyệt"), await Status());
        Assert.False((await Detail()).GetProperty("isOverdue").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/approve")).Status);
        Assert.Equal(("COMPLETED", "Hoàn thành"), await Status());
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_DetailShowsTheLatestRejectionToVicePrincipalAndTeamLead()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        async Task<JsonElement> MatrixOf(HttpClient actor) => Data(await Send(actor, HttpMethod.Get, $"/api/tasks/{taskId}")).GetProperty("matrix");
        async Task<(string? Comment, DateTime? At)> Stored() => await f.Db.TaskHistories.Where(history => history.TaskId == taskId)
            .OrderByDescending(history => history.Id)
            .Select(history => new ValueTuple<string?, DateTime?>(history.Content, history.CreateAt)).FirstOrDefaultAsync();

        var id = Id((await Send(tl, HttpMethod.Post, "/api/matrices", MatrixBody(f, taskId))).Body);
        Assert.Equal(JsonValueKind.Null, (await MatrixOf(tl)).GetProperty("rejectComment").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/reject", new { comment = "  Thêm câu vận dụng  " })).Status);

        // Bản nháp bị trả về: PHT không thấy tên nhưng vẫn thấy nhận xét từ chối.
        var stored = await Stored();
        foreach (var actor in new[] { pht, tl })
        {
            var matrix = await MatrixOf(actor);
            Assert.Equal("Thêm câu vận dụng", Text(matrix, "rejectComment"));
            Assert.Equal(stored.At!.Value, matrix.GetProperty("rejectedAt").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        }
        Assert.Equal(JsonValueKind.Null, (await MatrixOf(pht)).GetProperty("name").ValueKind);

        // Lần từ chối vẫn nằm trong task_history, nhưng chỉ bản nháp bị trả về mới hiện nó: đã duyệt thì không.
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/approve")).Status);
        Assert.Equal("Thêm câu vận dụng", (await Stored()).Comment);
        foreach (var actor in new[] { pht, tl })
        {
            var matrix = await MatrixOf(actor);
            Assert.Equal(JsonValueKind.Null, matrix.GetProperty("rejectComment").ValueKind);
            Assert.Equal(JsonValueKind.Null, matrix.GetProperty("rejectedAt").ValueKind);
        }
    }

    // Clients may still send semesterId; it is accepted and ignored (D10).
    private const ulong RemovedSemesterId = 999;

    private static object MatrixTaskBody(TaskFixture f, string assignee = "tl") => new
    {
        assignedToUserId = f.Id(assignee), academicContextId = f.ContextA, semesterId = RemovedSemesterId,
        dueAt = Day(5), name = "Ma trận Toán thử", description = "Nhiệm vụ thử phân quyền"
    };

    private static object MatrixBody(TaskFixture f, ulong taskId) => new
    {
        name = "Ma trận của nhiệm vụ", academicContextId = f.ContextA, semesterId = RemovedSemesterId, taskId, totalScore = 10,
        details = new[] { new { lessonId = f.LessonA1.Id, cognitiveLevel = "NHAN_BIET", questionCount = 2, percentage = 100 } }
    };

    private static string Day(int offset) => DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd'T'00:00:00");

    private static object QuestionBody(ulong performer,
        (ulong Lesson, int Knowledge, int Understanding, int Application)[] lessons,
        int due = 5, string name = "Biên soạn câu hỏi · Số và phép tính") => new
    {
        name,
        assignedToUserId = performer,
        dueAt = Day(due),
        lessons = lessons.Select(lesson => new
        {
            lessonId = lesson.Lesson, knowledge = lesson.Knowledge, understanding = lesson.Understanding, application = lesson.Application
        })
    };

    private static object ExamBody(ulong matrixId, ulong performer, int? due = 7, int papers = 2) => new
    {
        name = "Tạo bộ đề Toán 5 · Cuối học kỳ I",
        examMatrixId = matrixId,
        assignedToUserId = performer,
        dueAt = due is null ? null : Day(due.Value),
        paperCount = papers
    };

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body, body.GetType()) };
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Send(HttpClient client, HttpMethod method, string url, object body, HttpStatusCode expected)
    {
        var result = await Send(client, method, url, body);
        Assert.True(result.Status == expected, $"{(int)result.Status}: {result.Body}");
        return (HttpStatusCode.OK, result.Body);
    }

    private static JsonElement Data((HttpStatusCode Status, JsonElement Body) result)
    {
        Assert.True(result.Status == HttpStatusCode.OK, $"{(int)result.Status}: {result.Body}");
        return result.Body.GetProperty("data");
    }

    private static async Task<JsonElement[]> ListAsync(HttpClient client, string query = "") =>
        Data(await Send(client, HttpMethod.Get, "/api/tasks" + query)).GetProperty("items").EnumerateArray().ToArray();

    // Trả khoá trường bị báo lỗi (422 chỉ một trường trong mỗi kịch bản).
    private static string Invalid((HttpStatusCode Status, JsonElement Body) result)
    {
        Assert.True(result.Status == HttpStatusCode.UnprocessableEntity, $"{(int)result.Status}: {result.Body}");
        return Assert.Single(result.Body.GetProperty("error").GetProperty("details").EnumerateObject()).Name;
    }

    private static string Code((HttpStatusCode Status, JsonElement Body) result, HttpStatusCode expected)
    {
        Assert.True(result.Status == expected, $"{(int)result.Status}: {result.Body}");
        return Text(result.Body.GetProperty("error"), "code");
    }

    private static ulong Id(JsonElement element) => element.GetProperty("id").GetUInt64();

    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString()!;

    private static IEnumerable<string> Names(JsonElement array, string property) => array.EnumerateArray().Select(item => Text(item, property));

    private static (string Name, string? Role) Person(JsonElement element, string property) =>
        (Text(element.GetProperty(property), "fullName"), element.GetProperty(property).GetProperty("roleLabel").GetString());

    // Pause the old non-locking read after it has a snapshot, or the new locking read before acquiring its lock.
    private sealed class ReferenceReadBarrier(string table) : DbCommandInterceptor
    {
        private int armed;
        private int entered;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => armed = 1;

        private bool ReadsReference(DbCommand command) => armed == 1 &&
            (command.CommandText.Contains($"FROM {table}", StringComparison.OrdinalIgnoreCase) ||
             command.CommandText.Contains($"FROM `{table}`", StringComparison.OrdinalIgnoreCase));

        private async Task PauseAsync(CancellationToken ct)
        {
            if (Interlocked.CompareExchange(ref entered, 1, 0) != 0) return;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (ReadsReference(command) && command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase)) await PauseAsync(ct);
            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        {
            if (ReadsReference(command) && !command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase)) await PauseAsync(ct);
            return result;
        }
    }

    internal sealed class TaskFixture : IAsyncDisposable
    {
        private const string Prefix = "telo_task_test_";
        private const string Password = "Task-test-password-123";
        private readonly string database = Prefix + Guid.NewGuid().ToString("N");
        private readonly Dictionary<string, User> users = new();
        private WebApplicationFactory<Program> factory = null!;
        private DbCommandInterceptor? interceptor;

        public ApplicationDbContext Db { get; private set; } = null!;
        public School School { get; private set; } = null!;
        public Lesson LessonA1 { get; private set; } = null!;
        public Lesson LessonA2 { get; private set; } = null!;
        public Lesson LessonB { get; private set; } = null!;
        // academicContextId values (MatrixScope.Key): ContextA2 is a second scope of branch A (another grade).
        public ulong ContextA { get; private set; }
        public ulong ContextA2 { get; private set; }
        public ulong ContextB { get; private set; }
        public ExamMatrix ApprovedA { get; private set; } = null!;
        public ExamMatrix DraftA { get; private set; } = null!;
        public ExamMatrix ApprovedB { get; private set; } = null!;

        public static async Task<TaskFixture> CreateAsync(DbCommandInterceptor? interceptor = null)
        {
            var fixture = new TaskFixture { interceptor = interceptor };
            try { await fixture.InitializeAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public ulong Id(string username) => users[username].Id;

        public HttpClient CreateClient()
        {
            var client = factory.CreateClient();
            client.BaseAddress = new Uri("https://localhost");
            return client;
        }

        public async Task<HttpClient> LoginAsync(string username)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
            return client;
        }

        public async Task AddQuestionAsync(ulong taskId, ulong authorId)
        {
            var detail = await Db.QuestionTaskDetails.Where(detail => detail.QuestionTask.TaskId == taskId)
                .Select(detail => new { detail.Id, detail.QuestionTask.LessonId }).FirstAsync();
            Db.Questions.Add(new Question
            {
                QuestionTaskDetailId = detail.Id, LessonId = detail.LessonId, BankType = "TASK",
                Content = "Câu 1", Status = "DRAFT", CreatedByUserId = authorId
            });
            await Db.SaveChangesAsync();
        }

        // The team lead saves a draft: the matrix created with the task gets its first detail row.
        public async Task<ulong> AddTaskMatrixAsync(ulong taskId)
        {
            var matrixId = await Db.ExamMatrices.Where(matrix => matrix.TaskId == taskId).Select(matrix => matrix.Id).SingleAsync();
            Db.MatrixDetails.Add(new MatrixDetail
            {
                ExamMatrixId = matrixId, LessonId = LessonA1.Id, CognitiveLevel = MatrixCognitiveLevels.Knowledge,
                QuestionType = MatrixQuestionTypes.MultipleChoice, QuestionCount = 1, Percentage = 100
            });
            await Db.SaveChangesAsync();
            return matrixId;
        }

        public Task SetStatusAsync(ulong taskId, string status) =>
            Db.WorkTasks.Where(task => task.Id == taskId).ExecuteUpdateAsync(set => set.SetProperty(task => task.Status, status));

        private async Task InitializeAsync()
        {
            var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!) { Database = database };
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options);
            await Db.Database.MigrateAsync();

            var school = School = new School { Code = "TASK", Name = "Trường thử nhiệm vụ" };
            var branchA = new SchoolBranch { Code = "A1", Name = "Phân hiệu A", School = school };
            var branchB = new SchoolBranch { Code = "B1", Name = "Phân hiệu B", School = school };
            var pht = NewRole("PHT");
            var teamLead = NewRole("TEAM_LEAD");
            var teacher = NewRole("GIAO_VIEN");
            var student = NewRole("STUDENT");
            Account("pht", "Trần Thị Mai", branchA, pht);
            Account("pht2", "Phó Hiệu trưởng khác", branchA, pht);
            Account("principal", "Hiệu trưởng", null, NewRole("PRINCIPAL"));
            Account("tl", "Nguyễn Thị Lan", branchA, teamLead);
            Account("tl2", "Đỗ Văn Tổ", branchA, teamLead);
            Account("gv1", "Lê Thu Hà", branchA, teacher);
            Account("gv2", "Phạm Minh Anh", branchA, teacher);
            Account("gv3", "Vũ Văn Ba", branchA, teacher);
            Account("gvB", "Giáo viên phân hiệu B", branchB, teacher);
            Account("tlB", "Tổ trưởng phân hiệu B", branchB, teamLead);
            Account("nobranch", "Giáo viên chưa gán phân hiệu", null, teacher);
            Account("student", "Học sinh", branchA, student);
            Account("admin", "Quản trị", null, NewRole("ADMIN"));

            // A matrix scope exists through its chapters (no academic_contexts table any more).
            var subject = new Subject { Code = "TOAN_THU", Name = "Toán thử" };
            var grade = new GradeLevel { Name = "Khối thử" };
            var otherGrade = new GradeLevel { Name = "Khối khác" };
            var chapterA = new Chapter { SchoolBranch = branchA, GradeLevel = grade, Subject = subject, Title = "Số và phép tính" };
            var chapterA2 = new Chapter { SchoolBranch = branchA, GradeLevel = otherGrade, Subject = subject, Title = "Hình học" };
            var chapterB = new Chapter { SchoolBranch = branchB, GradeLevel = grade, Subject = subject, Title = "Số và phép tính" };
            LessonA1 = new Lesson { Chapter = chapterA, Number = 1, Title = "Phân số" };
            LessonA2 = new Lesson { Chapter = chapterA, Number = 2, Title = "Số thập phân" };
            LessonB = new Lesson { Chapter = chapterB, Title = "Phân số" };
            Db.AddRange(LessonA1, LessonA2, LessonB, chapterA2);
            Db.Users.AddRange(users.Values);
            Db.Students.Add(new Student { User = users["student"] });
            await Db.SaveChangesAsync();

            ContextA = new MatrixScope(branchA.Id, subject.Id, grade.Id).Key;
            ContextA2 = new MatrixScope(branchA.Id, subject.Id, otherGrade.Id).Key;
            ContextB = new MatrixScope(branchB.Id, subject.Id, grade.Id).Key;
            ExamMatrix NewMatrix(string name, string status, SchoolBranch branch) => new()
            {
                Name = name, Status = status, Branch = branch, Subject = subject, GradeLevel = grade, TotalScore = 10, CreatedByUserId = Id("pht")
            };
            ApprovedA = NewMatrix("Toán 5 · Cuối học kỳ I", MatrixStatusCodes.Approved, branchA);
            DraftA = NewMatrix("Ma trận nháp", MatrixStatusCodes.Draft, branchA);
            ApprovedB = NewMatrix("Ma trận phân hiệu B", MatrixStatusCodes.Approved, branchB);
            Db.ExamMatrices.AddRange(ApprovedA, DraftA, ApprovedB);
            await Db.SaveChangesAsync();

            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "task-integration-tests-signing-key-32-characters");
                builder.ConfigureServices(services =>
                {
                    if (interceptor is not null)
                    {
                        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                        services.AddDbContext<ApplicationDbContext>(options => options
                            .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
                            .AddInterceptors(interceptor));
                    }
                });
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (factory != null) await factory.DisposeAsync();
            if (Db == null) return;
            Assert.StartsWith(Prefix, database);
            Assert.Equal(database, Db.Database.GetDbConnection().Database);
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
        }

        private static Role NewRole(string code) => new() { Code = code, Name = code };

        private void Account(string username, string fullName, SchoolBranch? branch, Role role)
        {
            var user = new User { Username = username, FullName = fullName, SchoolBranch = branch };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            user.UserRoles.Add(new UserRole { Role = role });
            users[username] = user;
        }
    }
}
