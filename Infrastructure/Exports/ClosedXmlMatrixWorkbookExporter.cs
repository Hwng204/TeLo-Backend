using ClosedXML.Excel;
using Domain.Entities.QuestionBank;

namespace Infrastructure.Exports;

public sealed class ClosedXmlMatrixWorkbookExporter : IMatrixWorkbookExporter
{
    public byte[] Create(MatrixWorkbookModel matrix)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Matrix");

        sheet.Cell("A1").Value = "Ma trận đề thi";
        sheet.Cell("A2").Value = "Tên ma trận";
        sheet.Cell("B2").Value = SafeText(matrix.Name);
        sheet.Cell("A3").Value = "Trạng thái";
        sheet.Cell("B3").Value = SafeText(MatrixStatusCodes.Label(matrix.Status));
        sheet.Cell("A4").Value = "Mã nhiệm vụ";
        sheet.Cell("B4").Value = matrix.TaskId?.ToString() ?? string.Empty;
        sheet.Cell("A5").Value = "Ngữ cảnh học thuật";
        sheet.Cell("B5").Value = SafeText(matrix.ContextLabel);
        sheet.Cell("A6").Value = "Học kỳ";
        sheet.Cell("B6").Value = SafeText(matrix.SemesterName);
        sheet.Cell("A7").Value = "Tổng số câu";
        sheet.Cell("B7").Value = matrix.TotalQuestions;
        sheet.Cell("A8").Value = "Tổng điểm";
        sheet.Cell("B8").Value = matrix.TotalScore;

        var headerRow = 10;
        var headers = new[]
        {
            "Bài học",
            "Mức nhận thức",
            "Loại câu hỏi",
            "Số câu",
            "Tỷ lệ %",
            "Điểm"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(headerRow, column + 1).Value = headers[column];
        }

        for (var index = 0; index < matrix.Rows.Count; index++)
        {
            var detail = matrix.Rows[index];
            var row = headerRow + index + 1;
            sheet.Cell(row, 1).Value = SafeText(detail.LessonTitle);
            var level = MatrixCognitiveLevels.All
                .Where(item => item.Code == detail.CognitiveLevel)
                .Select(item => item.Label)
                .FirstOrDefault();
            sheet.Cell(row, 2).Value = SafeText(level ?? detail.CognitiveLevel);
            sheet.Cell(row, 3).Value = SafeText(
                detail.QuestionType == MatrixQuestionTypes.MultipleChoice
                    ? "Trắc nghiệm"
                    : detail.QuestionType);
            sheet.Cell(row, 4).Value = detail.QuestionCount;
            sheet.Cell(row, 5).Value = detail.Percentage;
            sheet.Cell(row, 6).Value = detail.CellScore;
        }

        sheet.Range("A1:B1").Merge().Style.Font.SetBold();
        sheet.Range($"A{headerRow}:F{headerRow}").Style.Font.SetBold();
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SafeText(string value) => WorkbookText.Safe(value);
}
