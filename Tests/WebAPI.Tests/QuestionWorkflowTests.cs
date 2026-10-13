using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WebAPI.Tests;

[Collection("Identity MySQL")]
public sealed class QuestionWorkflowTests
{
    [IdentityMySqlFact]
    public async Task FourSlotsWithUnusedChoices_SaveAsDraftAndSubmitOnlyWithAUsedCorrectChoice()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        using var lead = await f.LoginAsync("tl"); using var teacher = await f.LoginAsync("gv1");
        var task = await Ok(await lead.PostAsJsonAsync("/api/tasks/question", new { name = "Ô đáp án để trống", assignedToUserId = f.Id("gv1"), dueAt = DateTime.Today.AddDays(5), lessons = new[] { new { lessonId = f.LessonA1.Id, knowledge = 1, understanding = 0, application = 0 } } }));
        var root = $"/api/tasks/{task.GetProperty("id").GetUInt64()}";
        var package = await Ok(await teacher.GetAsync(root + "/question-package"));
        var detailId = package.GetProperty("targets")[0].GetProperty("detailId").GetUInt64();
        package = await Ok(await teacher.PostAsJsonAsync(root + "/questions", new { version = 0, detailId, content = "Số nào lớn hơn?", options = new[] { "", "2", "", "4" }, correctOption = "A" }));
        Assert.False(package.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsJsonAsync(root + "/question-package/submit", new { version = 1 })).StatusCode);
        var questionId = package.GetProperty("questions")[0].GetProperty("id").GetUInt64();
        package = await Ok(await teacher.PutAsJsonAsync(root + "/questions/" + questionId, new { version = 1, detailId, content = "Số nào lớn hơn?", options = new[] { "", "2", "", "4" }, correctOption = "D" }));
        Assert.True(package.GetProperty("canSubmit").GetBoolean());
        await Ok(await teacher.PostAsJsonAsync(root + "/question-package/submit", new { version = 2 }));
        package = await Ok(await lead.PostAsJsonAsync(root + "/question-package/approve", new { version = 3 }));
        Assert.Equal("COMPLETED", package.GetProperty("status").GetString());
        Assert.Equal(new[] { "", "2", "", "4" }, package.GetProperty("questions")[0].GetProperty("options").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(4, await f.Db.QuestionOptions.CountAsync());
    }
    [IdentityMySqlFact]
    public async Task ConcurrentWritesFromTheSameVersion_OnlyOnePersists()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        using var lead = await f.LoginAsync("tl"); using var teacher = await f.LoginAsync("gv1");
        var task = await Ok(await lead.PostAsJsonAsync("/api/tasks/question", new { name = "Hai tab cùng sửa", assignedToUserId = f.Id("gv1"), dueAt = DateTime.Today.AddDays(5), lessons = new[] { new { lessonId = f.LessonA1.Id, knowledge = 2, understanding = 0, application = 0 } } }));
        var id = task.GetProperty("id").GetUInt64(); var package = await Ok(await teacher.GetAsync($"/api/tasks/{id}/question-package")); var detailId = package.GetProperty("targets")[0].GetProperty("detailId").GetUInt64();
        var responses = await Task.WhenAll(Enumerable.Range(1, 2).Select(n => teacher.PostAsJsonAsync($"/api/tasks/{id}/questions", new { version = 0, detailId, content = $"Câu từ tab {n}", options = new[] { "1", "2", "3", "4" }, correctOption = "A" })));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).OrderBy(x => (int)x));
        Assert.Equal(1, await f.Db.Questions.CountAsync());
    }
    private static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {content}");
        return JsonDocument.Parse(content).RootElement.GetProperty("data").Clone();
    }

    [IdentityMySqlFact]
    public async Task WholePackage_RejectResubmitApprove_PreservesSnapshotsAndBlocksOtherAuthors()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        using var lead = await f.LoginAsync("tl"); using var teacher = await f.LoginAsync("gv1"); using var other = await f.LoginAsync("gv2");
        var assigned = await Ok(await lead.PostAsJsonAsync("/api/tasks/question", new { name = "Gói câu hỏi thử", assignedToUserId = f.Id("gv1"), dueAt = DateTime.Today.AddDays(5), lessons = new[] { new { lessonId = f.LessonA1.Id, knowledge = 1, understanding = 1, application = 0 } } }));
        var taskId = assigned.GetProperty("id").GetUInt64(); var root = $"/api/tasks/{taskId}";
        var package = await Ok(await teacher.GetAsync(root + "/question-package"));
        var targets = package.GetProperty("targets").EnumerateArray().Select(x => x.GetProperty("detailId").GetUInt64()).ToArray();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(root + "/question-package")).StatusCode);
        async Task<JsonElement> Save(ulong detailId, int version, string content) => await Ok(await teacher.PostAsJsonAsync(root + "/questions", new { version, detailId, content, options = new[] { "1", "2", "3", "4" }, correctOption = "B" }));
        package = await Save(targets[0], 0, "Câu hỏi ban đầu");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await teacher.PostAsJsonAsync(root + "/question-package/submit", new { version = 1 })).StatusCode);
        package = await Save(targets[1], 1, "Câu thông hiểu");
        Assert.True(package.GetProperty("canSubmit").GetBoolean());
        package = await Ok(await teacher.PostAsJsonAsync(root + "/question-package/submit", new { version = 2 }));
        Assert.Equal("PENDING_REVIEW", package.GetProperty("status").GetString());
        Assert.EndsWith("Z", package.GetProperty("submissions")[0].GetProperty("submittedAt").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.DeleteAsync(root + "/questions/" + package.GetProperty("questions")[0].GetProperty("id").GetUInt64() + "?version=3")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync(root + "/question-package/approve", new { version = 3 })).StatusCode);
        package = await Ok(await lead.PostAsJsonAsync(root + "/question-package/reject", new { version = 3, comment = "Sửa cách diễn đạt" }));
        Assert.Equal("NEEDS_REVISION", package.GetProperty("status").GetString());
        var questionId = package.GetProperty("questions")[0].GetProperty("id").GetUInt64();
        package = await Ok(await teacher.PutAsJsonAsync(root + "/questions/" + questionId, new { version = 4, detailId = targets[0], content = "Câu hỏi đã sửa", options = new[] { "1", "2", "3", "4" }, correctOption = "B" }));
        Assert.Equal("Câu hỏi ban đầu", package.GetProperty("submissions")[0].GetProperty("questions")[0].GetProperty("content").GetString());
        await Ok(await teacher.PostAsJsonAsync(root + "/question-package/submit", new { version = 5 }));
        package = await Ok(await lead.PostAsJsonAsync(root + "/question-package/approve", new { version = 6 }));
        Assert.Equal("COMPLETED", package.GetProperty("status").GetString());
        Assert.Equal(2, package.GetProperty("submissions").GetArrayLength());
        Assert.Equal(2, await f.Db.Questions.CountAsync(x => x.QuestionBankId != null && x.Status == "APPROVED"));
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.DeleteAsync(root + $"/questions/{questionId}?version=7")).StatusCode);
        using var pht = await f.LoginAsync("pht");
        var bank = await Ok(await pht.GetAsync("/api/question-banks/questions?bankType=exam"));
        Assert.Equal(2, bank.GetProperty("totalCount").GetInt32());
        Assert.EndsWith("Z", bank.GetProperty("items")[0].GetProperty("approvedAt").GetString());
        using var principal = await f.LoginAsync("principal"); using var otherBranch = await f.LoginAsync("gvB");
        Assert.Equal(2, (await Ok(await principal.GetAsync("/api/question-banks/questions"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await principal.PostAsync($"/api/question-banks/questions/{questionId}/move-to-practice", null)).StatusCode);
        Assert.Equal(0, (await Ok(await otherBranch.GetAsync("/api/question-banks/questions"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await otherBranch.GetAsync($"/api/question-banks/questions/{questionId}")).StatusCode);
        Assert.NotEmpty((await Ok(await teacher.GetAsync("/api/question-banks/options"))).GetProperty("subjects").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsync($"/api/question-banks/questions/{questionId}/move-to-practice", null)).StatusCode);
        var moved = await Ok(await pht.PostAsync($"/api/question-banks/questions/{questionId}/move-to-practice", null));
        Assert.Equal("practice", moved.GetProperty("bankType").GetString());
    }
}
