using ClosedXML.Excel;

namespace Infrastructure.Exports;

// One data row exactly as typed (trimmed). A row with empty lesson cells is a chapter without lessons.
public sealed record CurriculumImportRawRow(
    int RowNumber,
    string Grade,
    string Field,
    string ChapterCode,
    string ChapterTitle,
    string LessonCode,
    string LessonTitle);

// The file cannot be read as a chapter/lesson list (not an .xlsx, wrong columns, empty, too many rows).
public sealed class CurriculumImportFormatException(string message) : Exception(message);

// Builds the download template and reads a filled-in copy back.
public static class CurriculumWorkbook
{
    public const int MaxRows = 1000;
    public const string DataSheet = "ChuongBai";
    public const string GuideSheet = "HuongDan";

    // Column order is the contract with Read: A..F.
    public static readonly string[] Headers =
    [
        "Khối lớp (*)", "Lĩnh vực (*)", "Mã chương (*)", "Tên chương (*)", "Mã bài", "Tên bài"
    ];

    public static byte[] CreateTemplate(IReadOnlyList<string> grades, IReadOnlyList<string> fields)
    {
        using var workbook = new XLWorkbook();

        var data = workbook.Worksheets.Add(DataSheet);
        for (var column = 0; column < Headers.Length; column++)
        {
            data.Cell(1, column + 1).Value = Headers[column];
        }

        data.Row(1).Style.Font.SetBold();
        // Text format keeps codes such as "01" and stops Excel turning "1.2" into a number.
        data.Columns(1, Headers.Length).Style.NumberFormat.Format = "@";
        data.SheetView.FreezeRows(1);

        var guide = workbook.Worksheets.Add(GuideSheet);
        var row = 1;
        guide.Cell(row, 1).Value = "HƯỚNG DẪN NHẬP CHƯƠNG VÀ BÀI HỌC";
        guide.Cell(row, 1).Style.Font.SetBold();
        row += 2;
        foreach (var line in GuideLines)
        {
            guide.Cell(row++, 1).Value = line;
        }

        row++;
        guide.Cell(row, 1).Value = "Ví dụ";
        guide.Cell(row, 1).Style.Font.SetBold();
        row++;
        for (var column = 0; column < Headers.Length; column++)
        {
            guide.Cell(row, column + 1).Value = Headers[column];
        }

        guide.Range(row, 1, row, Headers.Length).Style.Font.SetBold();
        row++;
        var sampleGrade = grades.Count > 0 ? grades[0] : "Lớp 5";
        var sampleField = fields.Count > 0 ? fields[0] : "Số và phép tính";
        string[][] samples =
        [
            [sampleGrade, sampleField, "1", "Ôn tập và bổ sung", "1", "Ôn tập số tự nhiên"],
            [sampleGrade, sampleField, "1", "Ôn tập và bổ sung", "2", "Ôn tập các phép tính với số tự nhiên"],
            [sampleGrade, sampleField, "2", "Số thập phân", "", ""]
        ];
        foreach (var sample in samples)
        {
            for (var column = 0; column < sample.Length; column++)
            {
                guide.Cell(row, column + 1).Value = WorkbookText.Safe(sample[column]);
            }

            row++;
        }

        // Valid values live in their own columns so the data sheet can offer them as dropdowns.
        row += 2;
        guide.Cell(row, 1).Value = "Khối lớp hợp lệ";
        guide.Cell(row, 2).Value = "Lĩnh vực hợp lệ";
        guide.Range(row, 1, row, 2).Style.Font.SetBold();
        var firstValueRow = row + 1;
        for (var index = 0; index < grades.Count; index++)
        {
            guide.Cell(firstValueRow + index, 1).Value = WorkbookText.Safe(grades[index]);
        }

        for (var index = 0; index < fields.Count; index++)
        {
            guide.Cell(firstValueRow + index, 2).Value = WorkbookText.Safe(fields[index]);
        }

        if (grades.Count > 0)
        {
            data.Range(2, 1, MaxRows + 1, 1).CreateDataValidation().List(
                guide.Range(firstValueRow, 1, firstValueRow + grades.Count - 1, 1), true);
        }

        if (fields.Count > 0)
        {
            data.Range(2, 2, MaxRows + 1, 2).CreateDataValidation().List(
                guide.Range(firstValueRow, 2, firstValueRow + fields.Count - 1, 2), true);
        }

        data.Columns(1, Headers.Length).Width = 22;
        data.Column(4).Width = 36;
        data.Column(6).Width = 44;
        guide.Column(1).Width = 24;
        guide.Columns(2, Headers.Length).Width = 22;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<CurriculumImportRawRow> Read(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            if (!workbook.Worksheets.TryGetWorksheet(DataSheet, out var sheet))
            {
                throw new CurriculumImportFormatException(
                    $"Tệp không đúng mẫu: không có sheet {DataSheet}. Vui lòng tải mẫu mới và điền lại.");
            }

            for (var column = 0; column < Headers.Length; column++)
            {
                if (!string.Equals(
                        sheet.Cell(1, column + 1).GetString().Trim(),
                        Headers[column],
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new CurriculumImportFormatException(
                        "Tệp không đúng mẫu: tiêu đề cột đã bị đổi. Vui lòng tải mẫu mới và điền lại.");
                }
            }

            var rows = new List<CurriculumImportRawRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var cells = Enumerable.Range(1, Headers.Length)
                    .Select(column => sheet.Cell(rowNumber, column).GetFormattedString().Trim())
                    .ToArray();

                // Leftover formatting leaves blank rows behind; they are not data.
                if (cells.All(cell => cell.Length == 0))
                {
                    continue;
                }

                if (rows.Count >= MaxRows)
                {
                    throw new CurriculumImportFormatException(
                        $"Tệp có nhiều hơn {MaxRows} dòng dữ liệu. Vui lòng chia nhỏ tệp.");
                }

                rows.Add(new CurriculumImportRawRow(
                    rowNumber, cells[0], cells[1], cells[2], cells[3], cells[4], cells[5]));
            }

            if (rows.Count == 0)
            {
                throw new CurriculumImportFormatException("Tệp không có dòng dữ liệu nào.");
            }

            return rows;
        }
        catch (CurriculumImportFormatException)
        {
            throw;
        }
        catch (Exception)
        {
            // ClosedXML/OpenXML throw many exception types for a corrupt or non-xlsx file.
            throw new CurriculumImportFormatException(
                "Không đọc được tệp. Vui lòng dùng tệp Excel .xlsx theo mẫu.");
        }
    }

    private static readonly string[] GuideLines =
    [
        $"Nhập dữ liệu ở sheet {DataSheet}, bắt đầu từ dòng 2. Không đổi tên sheet, tên và thứ tự cột.",
        "Mỗi dòng là một bài học. Thông tin chương lặp lại ở mọi dòng thuộc chương đó.",
        "Chương chưa có bài: để trống Mã bài và Tên bài.",
        "Các cột có dấu (*) là bắt buộc. Mã bài và Tên bài phải điền cả hai hoặc bỏ trống cả hai.",
        "Khối lớp và Lĩnh vực chọn trong danh sách ở cuối trang này.",
        "Mã và tên chương không được trùng trong cùng khối lớp và lĩnh vực; mã và tên bài không được trùng trong cùng chương.",
        "Chương đã có trong hệ thống (cùng khối, lĩnh vực, mã và tên) thì bài mới được thêm vào chương đó.",
        "Chương có bài thuộc hai lĩnh vực: nhập chương hai lần, mỗi lĩnh vực một lần, cùng mã chương.",
        "Nếu có bất kỳ dòng lỗi nào, hệ thống không lưu gì. Sửa tệp rồi tải lên lại.",
        $"Tối đa {MaxRows} dòng mỗi tệp. Chỉ nhận tệp .xlsx."
    ];
}
