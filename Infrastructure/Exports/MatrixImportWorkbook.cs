using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Domain.Entities.QuestionBank;
using Infrastructure.Models;

namespace Infrastructure.Exports;

public sealed class MatrixImportFormatException(string message) : Exception(message);
public sealed record MatrixImportSheet(IReadOnlyList<string[]> Rows);

public static class MatrixImportWorkbook
{
    public const int MaxRows = 5000;
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public static readonly string[] Headers = ["Bài học", "Mức nhận thức", "Loại câu hỏi", "Số câu", "Tỷ lệ %", "Điểm", "ID bài học"];

    public static string LessonLabel(MatrixLessonOption lesson) =>
        $"Bài {lesson.Code}. {lesson.Title} — Chương {lesson.ChapterCode}. {lesson.ChapterTitle}";

    public static byte[] CreateTemplate(string? name, int totalScore, string contextLabel, string? semesterName,
        IReadOnlyList<MatrixLessonOption> lessons)
    {
        if (lessons.Count * 3 > MaxRows - 8)
            throw new MatrixImportFormatException($"Chương trình có quá nhiều bài học cho một tệp mẫu (tối đa {MaxRows} dòng).");

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Matrix");
        sheet.Cell(1, 1).Value = "Ma trận đề thi";
        string[][] metadata =
        [
            ["Tên ma trận", name ?? ""], ["Ngữ cảnh học thuật", contextLabel], ["Học kỳ", semesterName ?? ""],
            ["Tổng điểm", totalScore.ToString(CultureInfo.InvariantCulture)],
            ["Hướng dẫn", "Chỉ điền Số câu và Tỷ lệ %. Ô hoàn toàn trống được bỏ qua. Có lỗi thì không nhập bất kỳ dòng nào. Không dùng công thức."]
        ];
        for (var i = 0; i < metadata.Length; i++)
            for (var col = 0; col < 2; col++) sheet.Cell(i + 2, col + 1).Value = WorkbookText.Safe(metadata[i][col]);
        sheet.Cell(5, 2).Value = totalScore;
        for (var col = 0; col < Headers.Length; col++) sheet.Cell(8, col + 1).Value = Headers[col];
        var row = 9;
        foreach (var lesson in lessons)
        {
            foreach (var level in MatrixCognitiveLevels.All)
            {
                sheet.Cell(row, 1).Value = WorkbookText.Safe(LessonLabel(lesson));
                sheet.Cell(row, 2).Value = level.Label;
                sheet.Cell(row, 3).Value = "Trắc nghiệm";
                sheet.Cell(row, 7).Value = lesson.Id.ToString(CultureInfo.InvariantCulture);
                sheet.Range(row, 4, row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#edf2fd");
                row++;
            }
        }
        sheet.Range(1, 1, 1, 2).Merge().Style.Font.SetBold();
        sheet.Range(8, 1, 8, 7).Style.Font.SetBold();
        sheet.SheetView.FreezeRows(8);
        sheet.Column(1).Width = 65;
        sheet.Columns(2, 3).Width = 18;
        sheet.Columns(4, 6).Width = 12;
        sheet.Column(7).Hide();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static MatrixImportSheet Read(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (archive.Entries.Count > 1000 || archive.Entries.Sum(entry => entry.Length) > 50 * 1024 * 1024)
                    throw new MatrixImportFormatException("Tệp Excel có nội dung quá lớn sau khi giải nén.");
            }
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.First();
            var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
            var lastColumn = sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
            if (lastRow > MaxRows || lastColumn > 32)
                throw new MatrixImportFormatException($"Tệp vượt giới hạn {MaxRows} dòng hoặc 32 cột. Vui lòng chia nhỏ tệp.");
            if (lastRow == 0)
                throw new MatrixImportFormatException("Tệp không có dữ liệu. Vui lòng dùng tệp mẫu.");
            var rows = new List<string[]>();
            for (var row = 1; row <= lastRow; row++)
            {
                var cells = new string[lastColumn];
                for (var col = 1; col <= lastColumn; col++)
                {
                    var cell = sheet.Cell(row, col);
                    if (cell.HasFormula)
                        throw new MatrixImportFormatException($"Dòng {row}: Không dùng công thức trong tệp nhập; hãy điền giá trị trực tiếp.");
                    cells[col - 1] = cell.DataType == XLDataType.Number
                        ? cell.GetDouble().ToString("G15", CultureInfo.InvariantCulture)
                        : cell.GetString().Trim();
                    if (cell.DataType == XLDataType.Number &&
                        (Regex.Replace(cell.Style.NumberFormat.Format, "\"[^\"]*\"|\\\\.|_.|\\*.", "").Contains('%') ||
                         cell.Style.NumberFormat.NumberFormatId is 9 or 10))
                        cells[col - 1] = (cell.GetDouble() * 100).ToString("G15", CultureInfo.InvariantCulture) + "%";
                }
                rows.Add(cells);
            }
            return new MatrixImportSheet(rows);
        }
        catch (MatrixImportFormatException) { throw; }
        catch (Exception)
        {
            throw new MatrixImportFormatException("Không đọc được tệp. Vui lòng dùng tệp Excel .xlsx theo mẫu.");
        }
    }
}
