using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WebAPI.Tests;

public sealed class QuestionPaperIntegrationTests
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    { var text = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, text); return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone(); }
    [IdentityMySqlFact]
    public async Task SelectIntoAssignedPaper_PersistsAndChecksQuotaVersionAndFrozenSets()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        var task = new WorkTask { Name = "Nguồn câu hỏi", TaskType = "QUESTION", Status = "ASSIGNED", CreatedByUserId = f.Id("tl"), AssignedToUserId = f.Id("gv1") };
        var group = new QuestionTask { Task = task, LessonId = f.LessonA1.Id, Status = "ASSIGNED", AssignedQuestionCount = 1 };
        group.Details.Add(new() { CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 1 }); f.Db.QuestionTasks.Add(group);
        var cell = new MatrixDetail { ExamMatrixId = f.ApprovedA.Id, LessonId = f.LessonA1.Id, CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 1, Percentage = 100 }; f.Db.MatrixDetails.Add(cell); await f.Db.SaveChangesAsync();
        var author = new QuestionActor(f.Id("gv1"), true, false, false, false); var reviewer = new QuestionActor(f.Id("tl"), false, true, false, false);
        var service = new QuestionService(f.Db, TimeProvider.System);
        var draft = await service.SaveAsync(author, task.Id, null, new(0, group.Details.Single().Id, "2 + 3 = ?", ["4", "5", "6", "7"], "B", null), default);
        await service.SubmitAsync(author, task.Id, 1, default); await service.ReviewAsync(reviewer, task.Id, true, new(2, null, null), default);
        var questionId = draft.Questions.Single().Id;
        using var pht = await f.LoginAsync("pht"); using var lead = await f.LoginAsync("tl");
        var assigned = await Data(await pht.PostAsJsonAsync("/api/tasks/exam", new { name = "Chọn câu cho đề", examMatrixId = f.ApprovedA.Id, assignedToUserId = f.Id("tl"), dueAt = DateTime.Today.AddDays(5), paperCount = 2 }));
        var taskId = assigned.GetProperty("id").GetUInt64(); var paperId = assigned.GetProperty("exam").GetProperty("papers")[0].GetProperty("id").GetUInt64(); var url = $"/api/tasks/{taskId}/papers/{paperId}/questions";
        var selected = await Data(await lead.PostAsJsonAsync(url, new { version = 0, detailId = cell.Id, questionIds = new[] { questionId } }));
        Assert.Single(selected.GetProperty("questions").EnumerateArray()); Assert.Single((await Data(await lead.GetAsync(url))).GetProperty("questions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await lead.PostAsJsonAsync(url, new { version = 0, detailId = cell.Id, questionIds = new[] { questionId } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(url, new { version = 1, detailId = cell.Id, questionIds = new[] { questionId } })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await lead.PostAsJsonAsync(url, new { version = 1, detailId = cell.Id + 100, questionIds = new[] { questionId } })).StatusCode);
        var setId = await f.Db.ExamTaskPapers.Where(x => x.Id == paperId).Select(x => x.ExamSetId!.Value).SingleAsync();
        var set = await f.Db.ExamSets.SingleAsync(x => x.Id == setId); set.Status = "APPROVED"; set.ApprovedAt = DateTime.UtcNow; set.ApprovedByUserId = f.Id("pht"); await f.Db.SaveChangesAsync();
        Assert.False((await Data(await lead.GetAsync(url))).GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await lead.DeleteAsync(url + $"/{questionId}?version=1")).StatusCode);
        Assert.Equal(1, await f.Db.ExamSetQuestions.CountAsync(x => x.ExamSetId == setId));
    }
}
