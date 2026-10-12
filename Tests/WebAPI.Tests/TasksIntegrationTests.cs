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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        Assert.Null(await f.Db.WorkTasks.Where(task => task.Id == id).Select(task => task.ReviewerUserId).SingleAsync());
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

        // Dữ liệu cũ còn giáo viên kiểm duyệt: sửa nhiệm vụ thì người kiểm duyệt chuyển về Tổ trưởng giao việc.
        await f.Db.WorkTasks.Where(task => task.Id == id).ExecuteUpdateAsync(set => set.SetProperty(task => task.ReviewerUserId, (ulong?)f.Id("gv3")));
        Assert.Equal("Vũ Văn Ba", Person(Data(await Send(gv3, HttpMethod.Get, $"/api/tasks/{id}")), "reviewer").Name);
        var migrated = Data(await Send(tl, HttpMethod.Put, $"{QuestionUrl}/{id}", QuestionBody(f.Id("gv1"), [(a2, 1, 0, 0)], 7)));
        Assert.Equal(("Nguyễn Thị Lan", "Tổ trưởng"), Person(migrated, "reviewer"));
        Assert.Null(await f.Db.WorkTasks.Where(task => task.Id == id).Select(task => task.ReviewerUserId).SingleAsync());
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
    public async Task ExamTask_VicePrincipalAssignsAnApprovedMatrixOfTheBranchWithAPaperCount()
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
        Assert.Equal((f.ApprovedA.Id, "Toán 5 · Cuối học kỳ I", "Toán thử", "Khối thử", "2026-2027"),
            (Id(option), Text(option, "name"), Text(option, "subjectName"), Text(option, "gradeLevelName"), Text(option, "academicYearName")));
        Assert.Empty(options.GetProperty("teachers").EnumerateArray());

        Assert.Equal("examMatrixId", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.DraftA.Id, f.Id("tl")))));
        Assert.Equal("examMatrixId", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedB.Id, f.Id("tl")))));
        Assert.Equal("assignedToUserId", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("gv1")))));
        Assert.Equal("assignedToUserId", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tlB")))));
        Assert.Equal("paperCount", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"), papers: 0))));
        var tooMany = await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"), papers: 21));
        Assert.Equal("paperCount", Invalid(tooMany));
        Assert.Equal("Số đề cần tạo từ 1 đến 20.", tooMany.Body.GetProperty("error").GetProperty("details").GetProperty("paperCount")[0].GetString());
        Assert.Equal("dueAt", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"), due: null))));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(gv1, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))).Status);
        Assert.False(await f.Db.WorkTasks.AnyAsync());

        var created = Data(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"))));
        var id = Id(created);
        var exam = created.GetProperty("exam");
        Assert.Equal((f.ApprovedA.Id, "Toán 5 · Cuối học kỳ I", "Toán thử", "Khối thử", "2026-2027"),
            (exam.GetProperty("matrixId").GetUInt64(), Text(exam, "matrixName"), Text(exam, "subjectName"), Text(exam, "gradeLevelName"), Text(exam, "academicYearName")));
        Assert.False(exam.TryGetProperty("examId", out _));
        Assert.False(exam.TryGetProperty("examName", out _));
        Assert.Equal(new[] { "Đề 1", "Đề 2" }, Names(exam.GetProperty("papers"), "name"));
        Assert.All(exam.GetProperty("papers").EnumerateArray(), paper => Assert.Equal("Chưa tạo", Text(paper, "statusLabel")));
        Assert.Equal(f.ContextA.Id, created.GetProperty("scope").GetProperty("academicContextId").GetUInt64());
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
        Assert.Equal(1, await f.Db.ExamTaskPapers.CountAsync(paper => paper.TaskId == id));
        Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 9, 3)));
        Assert.DoesNotContain(await ListAsync(tl), item => Id(item) == id);
        Assert.Contains(await ListAsync(tl2), item => Id(item) == id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl2, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2")))).Status);

        // Ma trận nguồn đã lưu trữ: giữ nguyên thì vẫn sửa được hạn, nhưng không giao nhiệm vụ mới từ nó.
        await f.Db.ExamMatrices.Where(matrix => matrix.Id == f.ApprovedA.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(matrix => matrix.Status, MatrixStatusCodes.Archived));
        Assert.Equal(id, Id(Data(await Send(pht, HttpMethod.Put, $"{ExamUrl}/{id}", ExamBody(f.ApprovedA.Id, f.Id("tl2"), 12, 3)))));
        Assert.Equal("examMatrixId", Invalid(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))));

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
        // GB-58: trường bật email "Giao nhiệm vụ lập ma trận" gửi cho Tổ trưởng nhận việc.
        var emails = $"/api/schools/{f.School.Id}/emails";
        var template = Data(await Send(admin, HttpMethod.Post, emails + "/templates", new SaveEmailTemplateRequest
        {
            Code = "TASK_NOTICE", Name = "Thông báo nhiệm vụ", EventCode = "MATRIX_ASSIGNED",
            Subject = "Nhiệm vụ tại {{schoolName}}", Body = "{{actorName}} giao {{taskName}}, hạn {{dueAt}}. {{actionUrl}}"
        }, HttpStatusCode.Created));
        Data(await Send(pht, HttpMethod.Put, emails + "/configuration/MATRIX_ASSIGNED", new SaveEmailConfigRequest
        {
            EmailTemplateVersionId = template.GetProperty("currentRevision").GetProperty("id").GetUInt64(), IsActive = true,
            Targets = [new(null, f.Id("tl"), "INCLUDE"), new(null, f.Id("tl2"), "INCLUDE")]
        }));
        object Matrix(string assignee, ulong contextId, ulong? semesterId, int due, string name = "Ma trận Toán 5 - Cuối học kỳ I") =>
            new { assignedToUserId = f.Id(assignee), academicContextId = contextId, semesterId, dueAt = Day(due), name, description = "20 câu trắc nghiệm" };

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(pht, HttpMethod.Post, "/api/matrix-tasks", Matrix("tl", f.ContextA.Id, f.SemesterA.Id, -1))).Status);
        var created = await Send(pht, HttpMethod.Post, "/api/matrix-tasks", Matrix("tl", f.ContextA.Id, f.SemesterA.Id, 3));
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = Id(created.Body);

        var item = Assert.Single(await ListAsync(pht), row => Id(row) == id);
        Assert.Equal(("Ma trận", "Nguyễn Thị Lan", "Trần Thị Mai"), (Text(item, "taskTypeLabel"), Person(item, "performer").Name, Person(item, "reviewer").Name));
        Assert.True(item.GetProperty("canEdit").GetBoolean());

        var url = $"/api/tasks/matrix/{id}";
        Assert.Equal("assignedToUserId", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("gv1", f.ContextA.Id, null, 3))));
        Assert.Equal("academicContextId", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextB.Id, null, 3))));
        Assert.Equal("semesterId", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextA.Id, f.SemesterOther.Id, 3))));
        Assert.Equal("dueAt", Invalid(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextA.Id, null, -2))));

        Assert.Equal(1, await f.Db.Notifications.CountAsync(notice => notice.EventKey == $"matrix-task:{id}:assigned"));
        var updated = Data(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA.Id, f.SemesterA.Id, 4, "Ma trận Toán 5 - Giữa học kỳ I")));
        // Đổi người nhận và hạn: báo lại; chỉ sửa yêu cầu công việc thì không gửi thêm.
        var reassigned = await f.Db.Notifications.Where(notice => notice.EventKey != null && notice.EventKey.StartsWith($"matrix-task:{id}:updated:")).ToListAsync();
        Assert.Contains("Ma trận Toán 5 - Giữa học kỳ I", Assert.Single(reassigned).Content);
        Data(await Send(pht, HttpMethod.Put, url, new
        {
            assignedToUserId = f.Id("tl2"), academicContextId = f.ContextA.Id, semesterId = f.SemesterA.Id, dueAt = Day(4),
            name = "Ma trận Toán 5 - Giữa học kỳ I", description = "Chỉ đổi yêu cầu"
        }));
        Assert.Single(await f.Db.Notifications.Where(notice => notice.EventKey != null && notice.EventKey.StartsWith($"matrix-task:{id}:updated:")).ToListAsync());
        Assert.Equal(("Ma trận Toán 5 - Giữa học kỳ I", "Đỗ Văn Tổ"), (Text(updated, "name"), Person(updated, "performer").Name));
        var scope = updated.GetProperty("scope");
        Assert.Equal(("Toán thử", "Khối thử", "2026-2027", "Học kỳ I"), (Text(scope, "subjectName"), Text(scope, "gradeLevelName"), Text(scope, "academicYearName"), Text(scope, "semesterName")));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(tl2, HttpMethod.Put, url, Matrix("tl2", f.ContextA.Id, f.SemesterA.Id, 4))).Status);

        // Tổ trưởng lưu nháp ma trận: không đổi được người nhận và phạm vi; bản nháp vẫn riêng của Tổ trưởng (GB-47).
        var draftId = await f.AddTaskMatrixAsync(id, f.Id("tl2"));
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Put, url, Matrix("tl", f.ContextA.Id, f.SemesterA.Id, 4)), HttpStatusCode.Conflict));
        Assert.Equal("TASK_STARTED", Code(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA.Id, null, 4)), HttpStatusCode.Conflict));
        var renamed = Data(await Send(pht, HttpMethod.Put, url, Matrix("tl2", f.ContextA.Id, f.SemesterA.Id, 6, "Ma trận đổi tên")));
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
    public async Task LegacyMatrixTasks_OnlyParticipantsReadAndOnlyTheCreatorDeletes()
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
            var unrelated = await Send(actor, HttpMethod.Get, "/api/matrix-tasks");
            Assert.Equal(HttpStatusCode.OK, unrelated.Status);
            Assert.Empty(unrelated.Body.GetProperty("items").EnumerateArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(actor, HttpMethod.Get, $"/api/matrix-tasks/{id}")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(actor, HttpMethod.Delete, $"/api/matrix-tasks/{id}")).Status);
            Assert.Empty(await ListAsync(actor));
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Get, $"/api/matrix-tasks/{id}")).Status);
        Assert.Equal(id, Id(Assert.Single((await Send(pht, HttpMethod.Get, "/api/matrix-tasks")).Body.GetProperty("items").EnumerateArray())));
        Assert.Equal(HttpStatusCode.NoContent, (await Send(pht, HttpMethod.Delete, $"/api/matrix-tasks/{id}")).Status);
        Assert.False(await f.Db.WorkTasks.AnyAsync(task => task.Id == id));
    }

    [IdentityMySqlFact]
    public async Task LegacyMatrixTaskDelete_RejectsStartedStatusesEvenWithoutAMatrix()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        var id = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        foreach (var status in new[] { "IN_PROGRESS", "PENDING_REVIEW", "NEEDS_REVISION", "COMPLETED" })
        {
            await f.SetStatusAsync(id, status);
            Assert.Equal(HttpStatusCode.Conflict, (await Send(pht, HttpMethod.Delete, $"/api/matrix-tasks/{id}")).Status);
            Assert.True(await f.Db.WorkTasks.AnyAsync(task => task.Id == id && task.Status == status));
        }
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_UnrelatedVicePrincipalCannotCreateTheAssignedMatrix()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var pht2 = await f.LoginAsync("pht2");
        var id = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(pht2, HttpMethod.Post, "/api/matrices", MatrixBody(f, id))).Status);
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == id));
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
            name = "Tổng số câu quá lớn", academicContextId = f.ContextA.Id, semesterId = f.SemesterA.Id, taskId, totalScore = 10,
            details = new[]
            {
                new { lessonId = f.LessonA1.Id, cognitiveLevel = "NHAN_BIET", questionCount = uint.MaxValue, percentage = 50 },
                new { lessonId = f.LessonA1.Id, cognitiveLevel = "THONG_HIEU", questionCount = 1u, percentage = 50 }
            }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.Status);
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == taskId));
    }

    [IdentityMySqlFact]
    public async Task MatrixTask_ClosedYearKeepsCurrentScopeMetadataAndAllowsDeadlineEdits()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        await f.Db.AcademicYears.Where(year => year.Id == f.ContextA.AcademicYearId)
            .ExecuteUpdateAsync(set => set.SetProperty(year => year.Status, "CLOSED"));

        var scope = Data(await Send(pht, HttpMethod.Get, $"/api/tasks/{taskId}")).GetProperty("scope");
        Assert.Equal(f.ContextA.AcademicYearId, scope.GetProperty("academicYearId").GetUInt64());
        Assert.Equal(f.ContextA.SubjectId, scope.GetProperty("subjectId").GetUInt64());
        Assert.Equal(f.ContextA.GradeLevelId, scope.GetProperty("gradeLevelId").GetUInt64());
        var updated = Data(await Send(pht, HttpMethod.Put, $"/api/tasks/matrix/{taskId}", new
        {
            assignedToUserId = f.Id("tl"), academicContextId = f.ContextA.Id, semesterId = f.SemesterA.Id,
            dueAt = Day(10), name = "Gia hạn nhiệm vụ năm đã đóng", description = "Giữ nguyên phạm vi"
        }));
        Assert.Equal(f.ContextA.Id, updated.GetProperty("scope").GetProperty("academicContextId").GetUInt64());
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
        var barrier = new ReferenceReadBarrier("tasks");
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
        Assert.False(await f.Db.ExamMatrices.AnyAsync(matrix => matrix.TaskId == taskId));
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
        Assert.Equal(f.Id("pht"), await f.Db.WorkTasks.Where(task => task.Id == taskId).Select(task => task.UpdatedByUserId).SingleAsync());
    }

    [IdentityMySqlFact]
    public async Task TaskMatrix_DetailShowsTheLatestRejectionToVicePrincipalAndTeamLead()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        var taskId = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
        async Task<JsonElement> MatrixOf(HttpClient actor) => Data(await Send(actor, HttpMethod.Get, $"/api/tasks/{taskId}")).GetProperty("matrix");
        async Task<(string? Comment, DateTime? At)> Stored() => await f.Db.ExamMatrices.Where(matrix => matrix.TaskId == taskId)
            .Select(matrix => new ValueTuple<string?, DateTime?>(matrix.RejectComment, matrix.RejectedAt)).SingleAsync();

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

        // Nộp lại xóa dấu từ chối trên ma trận; detail theo đúng dữ liệu đó sau khi duyệt.
        Assert.Equal(HttpStatusCode.OK, (await Send(tl, HttpMethod.Post, $"/api/matrices/{id}/submit")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{id}/approve")).Status);
        stored = await Stored();
        foreach (var actor in new[] { pht, tl })
        {
            var matrix = await MatrixOf(actor);
            Assert.Equal(stored.Comment, matrix.GetProperty("rejectComment").ValueKind == JsonValueKind.Null ? null : Text(matrix, "rejectComment"));
            Assert.Equal(stored.At is null, matrix.GetProperty("rejectedAt").ValueKind == JsonValueKind.Null);
        }
    }

    [IdentityMySqlFact]
    public async Task TaskMigration_NormalizesLegacyStatusesBothWays()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        async Task<ulong> MatrixTask(string? matrixStatus, string status, bool rejected = false)
        {
            var id = Id((await Send(pht, HttpMethod.Post, "/api/matrix-tasks", MatrixTaskBody(f))).Body);
            if (matrixStatus is not null)
            {
                var matrixId = await f.AddTaskMatrixAsync(id, f.Id("tl"));
                // Từ chối không kèm nhận xét vẫn để lại dấu rejected_at.
                await f.Db.ExamMatrices.Where(matrix => matrix.Id == matrixId).ExecuteUpdateAsync(set => set
                    .SetProperty(matrix => matrix.Status, matrixStatus)
                    .SetProperty(matrix => matrix.RejectedAt, rejected ? DateTime.UtcNow : (DateTime?)null));
            }
            await f.SetStatusAsync(id, status);
            return id;
        }

        var drafting = await MatrixTask(MatrixStatusCodes.Draft, "IN_PROGRESS");
        var revising = await MatrixTask(MatrixStatusCodes.Draft, "NEEDS_REVISION", rejected: true);
        var reviewing = await MatrixTask(MatrixStatusCodes.Submitted, "PENDING_REVIEW");
        var assigned = await MatrixTask(null, "ASSIGNED");
        var exam = Id(Data(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")))));
        await f.SetStatusAsync(exam, "PENDING_REVIEW");
        var question = Id(Data(await Send(tl, HttpMethod.Post, QuestionUrl, QuestionBody(f.Id("gv1"), [(f.LessonA1.Id, 1, 0, 0)]))));
        await f.SetStatusAsync(question, "PENDING_REVIEW");
        ulong[] ids = [drafting, revising, reviewing, assigned, exam, question];
        async Task<string[]> Statuses()
        {
            var result = new List<string>();
            foreach (var id in ids)
                result.Add(await f.Db.Database.SqlQuery<string>($"SELECT status AS Value FROM tasks WHERE id = {id}").SingleAsync());
            return [.. result];
        }

        var migrator = f.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006045506_RemoveAdmissionDate");
        Assert.Equal(new[] { "ASSIGNED", "ASSIGNED", "SUBMITTED", "ASSIGNED", "SUBMITTED", "PENDING_REVIEW" }, await Statuses());

        await migrator.MigrateAsync();
        Assert.Equal(new[] { "IN_PROGRESS", "NEEDS_REVISION", "PENDING_REVIEW", "ASSIGNED", "PENDING_REVIEW", "PENDING_REVIEW" }, await Statuses());
    }

    [IdentityMySqlFact]
    public async Task ExamTask_CreationRechecksApprovalAfterAConcurrentMatrixArchive()
    {
        var barrier = new ReferenceReadBarrier("exam_matrices");
        await using var f = await TaskFixture.CreateAsync(barrier);
        using var pht = await f.LoginAsync("pht");
        barrier.Arm();
        var creation = Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl")));
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, (await Send(pht, HttpMethod.Post, $"/api/matrices/{f.ApprovedA.Id}/archive")).Status);
        }
        finally { barrier.Release.TrySetResult(); }

        Assert.Equal("VALIDATION_ERROR", Code(await creation.WaitAsync(TimeSpan.FromSeconds(10)), HttpStatusCode.UnprocessableEntity));
        Assert.False(await f.Db.WorkTasks.AnyAsync());
    }

    [IdentityMySqlFact]
    public async Task TaskMigration_RefusesMultipleLessonRollbackBeforeDroppingExamPapers()
    {
        await using var f = await TaskFixture.CreateAsync();
        using var pht = await f.LoginAsync("pht");
        using var tl = await f.LoginAsync("tl");
        Data(await Send(tl, HttpMethod.Post, QuestionUrl,
            QuestionBody(f.Id("gv1"), [(f.LessonA1.Id, 1, 0, 0), (f.LessonA2.Id, 1, 0, 0)])));
        Data(await Send(pht, HttpMethod.Post, ExamUrl, ExamBody(f.ApprovedA.Id, f.Id("tl"))));

        var error = await Assert.ThrowsAsync<MySqlException>(() =>
            f.Db.GetService<IMigrator>().MigrateAsync("20261006045506_RemoveAdmissionDate"));
        Assert.Equal(1062, error.Number);
        Assert.Equal(2, await f.Db.ExamTaskPapers.CountAsync());
        Assert.Equal(2, await f.Db.QuestionTasks.CountAsync());
        Assert.Contains("20261010180003_AddTaskReviewersAndExamPapers", await f.Db.Database.GetAppliedMigrationsAsync());
    }

    private static object MatrixTaskBody(TaskFixture f, string assignee = "tl") => new
    {
        assignedToUserId = f.Id(assignee), academicContextId = f.ContextA.Id, semesterId = f.SemesterA.Id,
        dueAt = Day(5), name = "Ma trận Toán thử", description = "Nhiệm vụ thử phân quyền"
    };

    private static object MatrixBody(TaskFixture f, ulong taskId) => new
    {
        name = "Ma trận của nhiệm vụ", academicContextId = f.ContextA.Id, semesterId = f.SemesterA.Id, taskId, totalScore = 10,
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

    private sealed class TaskFixture : IAsyncDisposable
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
        public AcademicContext ContextA { get; private set; } = null!;
        public AcademicContext ContextB { get; private set; } = null!;
        public Semester SemesterA { get; private set; } = null!;
        public Semester SemesterOther { get; private set; } = null!;
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
            var detailId = await Db.QuestionTaskDetails.Where(detail => detail.QuestionTask.TaskId == taskId)
                .Select(detail => detail.Id).FirstAsync();
            Db.Questions.Add(new Question { QuestionTaskDetailId = detailId, Content = "Câu 1", Status = "DRAFT", CreatedByUserId = authorId });
            await Db.SaveChangesAsync();
        }

        public async Task<ulong> AddTaskMatrixAsync(ulong taskId, ulong teamLeadId)
        {
            var matrix = new ExamMatrix
            {
                Name = "Nháp của Tổ trưởng", Status = MatrixStatusCodes.Draft, TaskId = taskId, AcademicContextId = ContextA.Id,
                TotalScore = 10, CreatedAt = DateTime.UtcNow, CreatedByUserId = teamLeadId
            };
            Db.ExamMatrices.Add(matrix);
            await Db.SaveChangesAsync();
            return matrix.Id;
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

            var year = new AcademicYear { Name = "2026-2027", StartDate = new(2026, 8, 1), EndDate = new(2027, 6, 30) };
            year.AssignCode("TASK_TEST_2026");
            var otherYear = new AcademicYear { Name = "2027-2028", StartDate = new(2027, 8, 1), EndDate = new(2028, 6, 30) };
            otherYear.AssignCode("TASK_TEST_2027");
            SemesterA = new Semester { AcademicYear = year, Order = 1, Name = "Học kỳ I", StartDate = new(2026, 8, 1), EndDate = new(2027, 1, 15) };
            SemesterOther = new Semester { AcademicYear = otherYear, Order = 1, Name = "Học kỳ I", StartDate = new(2027, 8, 1), EndDate = new(2028, 1, 15) };
            var subject = new Subject { Name = "Toán thử" };
            var field = new SubjectField { Subject = subject, Name = "Số và phép tính" };
            var grade = new GradeLevel { Name = "Khối thử" };
            var chapterA = new Chapter { SchoolBranch = branchA, GradeLevel = grade, Field = field, Code = "I", Title = "Số và phép tính", SortOrder = 1 };
            var chapterB = new Chapter { SchoolBranch = branchB, GradeLevel = grade, Field = field, Code = "I", Title = "Số và phép tính", SortOrder = 1 };
            LessonA1 = new Lesson { Chapter = chapterA, Code = "1", Title = "Phân số", SortOrder = 1 };
            LessonA2 = new Lesson { Chapter = chapterA, Code = "2", Title = "Số thập phân", SortOrder = 2 };
            LessonB = new Lesson { Chapter = chapterB, Code = "1", Title = "Phân số", SortOrder = 1 };
            ContextA = new AcademicContext { AcademicYear = year, School = school, Subject = subject, GradeLevel = grade, SchoolBranch = branchA };
            ContextB = new AcademicContext { AcademicYear = year, School = school, Subject = subject, GradeLevel = grade, SchoolBranch = branchB };
            ApprovedA = NewMatrix("Toán 5 · Cuối học kỳ I", MatrixStatusCodes.Approved, ContextA);
            DraftA = NewMatrix("Ma trận nháp", MatrixStatusCodes.Draft, ContextA);
            ApprovedB = NewMatrix("Ma trận phân hiệu B", MatrixStatusCodes.Approved, ContextB);
            Db.AddRange(LessonA1, LessonA2, LessonB, SemesterA, SemesterOther, ApprovedA, DraftA, ApprovedB);
            Db.Users.AddRange(users.Values);
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
                    foreach (var worker in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                                 && descriptor.ImplementationType == typeof(WebAPI.EmailDeliveryWorker)).ToArray())
                        services.Remove(worker);
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

        private static Role NewRole(string code) => new() { Code = code, Name = code, IsSystem = true };

        private void Account(string username, string fullName, SchoolBranch? branch, Role role)
        {
            var user = new User { Username = username, FullName = fullName, Email = username + "@example.invalid", SchoolBranch = branch };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
            user.UserRoles.Add(new UserRole { Role = role });
            users[username] = user;
        }

        private static ExamMatrix NewMatrix(string name, string status, AcademicContext context) => new()
        {
            Name = name, Status = status, AcademicContext = context, TotalScore = 10, CreatedAt = DateTime.UtcNow
        };
    }
}
