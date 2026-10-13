using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WebAPI.Tests;

[Collection("Identity MySQL")]
public sealed class QuestionImportIntegrationTests
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(); Assert.True(response.IsSuccessStatusCode, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }

    [IdentityMySqlFact]
    public async Task GuideDownloadIsScopedToTheAssignedTeacher_AndBlankCorrectChoiceCannotBeImported()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        using var lead = await f.LoginAsync("tl"); using var teacher = await f.LoginAsync("gv1"); using var other = await f.LoginAsync("gv2");
        var task = await Data(await lead.PostAsJsonAsync("/api/tasks/question", new { name = "Excel đơn giản", assignedToUserId = f.Id("gv1"), dueAt = DateTime.Today.AddDays(5), lessons = new[] { new { lessonId = f.LessonA1.Id, knowledge = 1, understanding = 0, application = 0 } } }));
        var root = $"/api/tasks/{task.GetProperty("id").GetUInt64()}/questions/import";
        var guide = await teacher.GetAsync(root + "/guide.xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", guide.Content.Headers.ContentType?.MediaType);
        using var guideBook = new XLWorkbook(new MemoryStream(await guide.Content.ReadAsByteArrayAsync()));
        Assert.True(guideBook.TryGetWorksheet("Ví dụ", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(root + "/guide.xlsx")).StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await teacher.GetByteArrayAsync(root + "/template.xlsx")));
        var sheet = workbook.Worksheet("Câu hỏi");
        sheet.Cell(5, 1).Value = "Câu thử"; sheet.Cell(5, 2).Value = "1"; sheet.Cell(5, 4).Value = "3"; sheet.Cell(5, 6).Value = "B";
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        async Task<HttpResponseMessage> Upload(bool confirm)
        {
            using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(stream.ToArray()), "file", "cau-hoi.xlsx");
            return await teacher.PostAsync(root + (confirm ? "" : "/preview") + "?version=0", form);
        }
        foreach (var confirm in new[] { false, true })
        {
            var result = await Data(await Upload(confirm));
            Assert.Contains(result.GetProperty("errors").EnumerateArray(), e => e.GetProperty("column").GetString() == "Đáp án đúng" && e.GetProperty("message").GetString()!.Contains("đang để trống"));
            Assert.Equal(0, await f.Db.Questions.CountAsync());
        }
    }
    [IdentityMySqlFact]
    public async Task PreviewDoesNotPersist_ConfirmAppendsAtomically_StaleRetryDoesNotDuplicate()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        using var lead = await f.LoginAsync("tl"); using var teacher = await f.LoginAsync("gv1");
        var task = await Data(await lead.PostAsJsonAsync("/api/tasks/question", new { name = "Import câu hỏi", assignedToUserId = f.Id("gv1"), dueAt = DateTime.Today.AddDays(5), lessons = new[] { new { lessonId = f.LessonA1.Id, knowledge = 2, understanding = 1, application = 0 } } }));
        var id = task.GetProperty("id").GetUInt64(); var root = $"/api/tasks/{id}";
        var package = await Data(await teacher.GetAsync(root + "/question-package")); var detailId = package.GetProperty("targets")[0].GetProperty("detailId").GetUInt64();
        await Data(await teacher.PostAsJsonAsync(root + "/questions", new { version = 0, detailId, content = "Câu thủ công", options = new[] { "1", "2", "3", "4" }, correctOption = "B" }));
        var template = await teacher.GetByteArrayAsync(root + "/questions/import/template.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(template)); var sheet = workbook.Worksheet("Câu hỏi");
        foreach (var row in new[] { 5, 7 }) { sheet.Cell(row, 1).Value = $"Câu nhập Excel {row}"; sheet.Cell(row, 2).Value = "1"; sheet.Cell(row, 4).Value = "3"; sheet.Cell(row, 6).Value = "C"; }
        using var stream = new MemoryStream(); workbook.SaveAs(stream); var bytes = stream.ToArray();
        async Task<HttpResponseMessage> Upload(string path, int version)
        { using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes), "file", "cau-hoi.xlsx"); return await teacher.PostAsync(root + path + "?version=" + version, form); }
        var preview = await Data(await Upload("/questions/import/preview", 1)); Assert.Empty(preview.GetProperty("errors").EnumerateArray()); Assert.Equal(2, preview.GetProperty("questions").GetArrayLength());
        Assert.Equal(1, await f.Db.Questions.CountAsync());
        var imported = await Data(await Upload("/questions/import", 1)); Assert.Equal(3, imported.GetProperty("package").GetProperty("questions").GetArrayLength());
        Assert.True(imported.GetProperty("package").GetProperty("canSubmit").GetBoolean());
        var optional = imported.GetProperty("package").GetProperty("questions").EnumerateArray().Single(q => q.GetProperty("content").GetString() == "Câu nhập Excel 5");
        Assert.Equal("", optional.GetProperty("options")[1].GetString());
        Assert.Equal("", optional.GetProperty("options")[3].GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Upload("/questions/import", 1)).StatusCode);
        var overQuota = await Data(await Upload("/questions/import/preview", 2)); Assert.NotEmpty(overQuota.GetProperty("errors").EnumerateArray());
        Assert.Equal(3, await f.Db.Questions.CountAsync());
    }
}
