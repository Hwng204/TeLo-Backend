using ClosedXML.Excel;
using Infrastructure.Exports;
using Xunit;

namespace Application.Tests;

public sealed class QuestionImportTests
{
    private static byte[] File(bool formula = false)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Câu hỏi");
        string[] headers = ["Mã yêu cầu", "Bài học", "Mức nhận thức", "Nội dung", "A", "B", "C", "D", "Đáp án đúng", "Giải thích"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "1"; sheet.Cell(2, 2).Value = "Khối 5 · I/1 · Phân số"; sheet.Cell(2, 3).Value = "Nhận biết";
        if (formula) sheet.Cell(2, 4).FormulaA1 = "1+1"; else sheet.Cell(2, 4).Value = "2 + 3 = ?";
        for (var i = 0; i < 4; i++) sheet.Cell(2, i + 5).Value = (i + 4).ToString();
        sheet.Cell(2, 9).Value = "b";
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }
    [Fact]
    public void TemplateRoundTripPreservesContextAnswersAndUnicode()
    {
        var row = Assert.Single(QuestionImportWorkbook.Read("cau-hoi.xlsx", File()));
        Assert.Equal(1ul, row.TargetId); Assert.Equal("Nhận biết", row.Level); Assert.Equal("B", row.Answer);
        Assert.Equal(new[] { "4", "5", "6", "7" }, row.Options);
    }
    [Fact]
    public void FormulaInputsAreRejected() => Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("q.xlsx", File(true)));

    [Fact]
    public void SimplifiedTemplateHasOneInputSheetWithPrefilledGroupsAndNoExampleQuestions()
    {
        var bytes = QuestionImportWorkbook.Template([new(1, "Phân số", "Nhận biết", 2), new(2, "Số thập phân", "Vận dụng", 1)]);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Câu hỏi", sheet.Name);
        Assert.Equal("Nội dung câu hỏi *", sheet.Cell(3, 1).GetString());
        Assert.Equal("Đáp án đúng *", sheet.Cell(3, 6).GetString());
        Assert.True(sheet.Column(8).IsHidden);
        Assert.Contains("Phân số", sheet.Cell(4, 1).GetString());
        Assert.Contains("2 câu", sheet.Cell(4, 1).GetString());
        Assert.Contains("Số thập phân", sheet.Cell(7, 1).GetString());
        Assert.Empty(QuestionImportWorkbook.Read("q.xlsx", bytes));
    }

    private static byte[] SimplifiedFile(Action<IXLWorksheet>? edit = null)
    {
        using var workbook = new XLWorkbook(new MemoryStream(QuestionImportWorkbook.Template([new(1, "Phân số", "Nhận biết", 2), new(2, "Số thập phân", "Vận dụng", 1)])));
        var sheet = workbook.Worksheet("Câu hỏi");
        sheet.Cell(5, 1).Value = "Phân số nào bằng 1/2?";
        sheet.Cell(5, 2).Value = "2/4"; sheet.Cell(5, 4).Value = "1/3"; sheet.Cell(5, 6).Value = "a";
        sheet.Cell(8, 1).Value = "2,5 − 0,8 = ?";
        sheet.Cell(8, 3).Value = "1,7"; sheet.Cell(8, 5).Value = "3,3"; sheet.Cell(8, 6).Value = "B";
        edit?.Invoke(sheet);
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    [Fact]
    public void SimplifiedRoundTripKeepsUnusedSlotsAndMapsEachPrefilledGroup()
    {
        var rows = QuestionImportWorkbook.Read("q.xlsx", SimplifiedFile());
        Assert.Equal(2, rows.Length);
        Assert.Equal(1ul, rows[0].TargetId); Assert.Equal(2ul, rows[1].TargetId);
        Assert.Equal(new[] { "2/4", "", "1/3", "" }, rows[0].Options);
        Assert.Equal("A", rows[0].Answer); Assert.Equal("B", rows[1].Answer);
        Assert.True(Application.Common.QuestionRules.IsComplete(rows[0].Content, rows[0].Options, rows[0].Answer));
        Assert.True(Application.Common.QuestionRules.IsComplete(rows[1].Content, rows[1].Options, rows[1].Answer));
    }

    [Fact]
    public void SimplifiedFileRejectsFormulaAndChangedGroupContext()
    {
        Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("q.xlsx", SimplifiedFile(s => s.Cell(5, 1).FormulaA1 = "1+1")));
        Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("q.xlsx", SimplifiedFile(s => s.Cell(5, 8).Value = "2")));
    }

    [Fact]
    public void TooManyQuestionsAreRejectedInsteadOfSilentlyTruncatingTemplate()
    {
        Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Template([new(1, "Phân số", "Nhận biết", 5001)]));
    }

    [Fact]
    public void GuideIncludesValidExamplesAndCannotBeImportedAsTeacherQuestions()
    {
        var bytes = QuestionImportWorkbook.Guide();
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(new[] { "Hướng dẫn", "Ví dụ" }, workbook.Worksheets.Select(s => s.Name).ToArray());
        var examples = workbook.Worksheet("Ví dụ");
        for (var row = 4; row <= 7; row++)
        {
            var options = Enumerable.Range(2, 4).Select(col => examples.Cell(row, col).GetString()).ToArray();
            Assert.True(Application.Common.QuestionRules.IsComplete(examples.Cell(row, 1).GetString(), options, examples.Cell(row, 6).GetString()));
        }
        Assert.Contains("file hướng dẫn", Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("guide.xlsx", bytes)).Message);
    }
    [Fact]
    public void WrongExtensionAndCorruptArchiveAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("q.xls", File()));
        Assert.Throws<InvalidDataException>(() => QuestionImportWorkbook.Read("q.xlsx", [1, 2, 3]));
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-13T17:01:00Z"); }
    [Fact]
    public void DeadlineUsesSchoolTimezoneAtMidnight() => Assert.Equal(new DateOnly(2026, 10, 14), Application.Common.TaskRules.Today(new Clock()));
}
