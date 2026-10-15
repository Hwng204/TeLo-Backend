using ClosedXML.Excel;

namespace Infrastructure.Exports;

// One table row exactly as typed (trimmed), after fill-down. A row with empty lesson cells is a chapter without lessons.
public sealed record CurriculumImportRawRow(
    int RowNumber,
    string ChapterNumber,
    string ChapterTitle,
    string LessonNumber,
    string LessonTitle);

// A filled-in template: the grade (B4) and subject (B5) chosen once apply to every row.
public sealed record CurriculumImportSheet(string Grade, string Field, IReadOnlyList<CurriculumImportRawRow> Rows);

// The file cannot be read as a chapter/lesson list (not an .xlsx, wrong layout, empty, too many rows).
public sealed class CurriculumImportFormatException(string message) : Exception(message);

// Builds the download template and reads a filled-in copy back.
public static class CurriculumWorkbook
{
    public const int MaxRows = 1000;
    public const string DataSheet = "ChuongBai";
    public const string GuideSheet = "HuongDan";
    public const string ListSheet = "DanhMuc";

    // Cell positions are the contract with Read: labels in A4/A5 with the choices in B4/B5, table header in row 7.
    public const int GradeRow = 4;
    public const int FieldRow = 5;
    public const int HeaderRow = 7;
    public const string GradeLabel = "Khối lớp (*)";
    public const string FieldLabel = "Môn học (*)";
    public static readonly string[] Headers = ["Số chương (*)", "Tên chương (*)", "Số bài", "Tên bài"];

    private const int FirstDataRow = HeaderRow + 1;
    private static readonly XLColor Accent = XLColor.FromHtml("#668ee1");

    public static byte[] CreateTemplate(IReadOnlyList<string> grades, IReadOnlyList<string> fields)
    {
        using var workbook = new XLWorkbook();
        var data = workbook.Worksheets.Add(DataSheet);
        var guide = workbook.Worksheets.Add(GuideSheet);
        var lists = workbook.Worksheets.Add(ListSheet);
        lists.Hide();

        data.Cell(1, 1).Value = "NHẬP CHƯƠNG VÀ BÀI HỌC";
        data.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#26364a"));
        data.Range(1, 1, 1, Headers.Length).Merge();
        data.Cell(2, 1).Value =
            $"Chọn Khối lớp và Môn học, rồi nhập chương và bài ở bảng bên dưới. Xem hướng dẫn ở sheet {GuideSheet}.";
        data.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#5b6b7f"));
        data.Range(2, 1, 2, Headers.Length).Merge();

        AddChoice(data, GradeRow, GradeLabel, lists, 1, grades,
            "Khối lớp phải chọn trong danh sách.");
        AddChoice(data, FieldRow, FieldLabel, lists, 2, fields,
            "Môn học phải chọn trong danh sách.");

        for (var column = 0; column < Headers.Length; column++)
        {
            data.Cell(HeaderRow, column + 1).Value = Headers[column];
        }

        data.Range(HeaderRow, 1, HeaderRow, Headers.Length).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(Accent)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);

        var lastDataRow = FirstDataRow + MaxRows - 1;
        var line = XLColor.FromHtml("#d5dde8");
        data.Range(FirstDataRow, 1, lastDataRow, Headers.Length).Style
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(line)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(line);

        // Number columns keep the General format: a whole-number rule rejects entries stored as text.
        foreach (var column in new[] { 1, 3 })
        {
            var numbers = data.Range(FirstDataRow, column, lastDataRow, column);
            numbers.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            var validation = numbers.CreateDataValidation();
            validation.WholeNumber.Between(1, 999);
            validation.ErrorStyle = XLErrorStyle.Stop;
            validation.ErrorMessage = "Nhập số nguyên từ 1 đến 999.";
        }

        // Text format stops Excel turning a title such as "1.2" into a number.
        data.Column(2).Style.NumberFormat.Format = "@";
        data.Column(4).Style.NumberFormat.Format = "@";

        double[] widths = [16, 44, 10, 52];
        for (var column = 0; column < widths.Length; column++)
        {
            data.Column(column + 1).Width = widths[column];
            guide.Column(column + 1).Width = widths[column];
        }

        data.SheetView.FreezeRows(HeaderRow);

        var row = 1;
        guide.Cell(row, 1).Value = "HƯỚNG DẪN NHẬP CHƯƠNG VÀ BÀI HỌC";
        guide.Cell(row, 1).Style.Font.SetBold();
        row += 2;
        foreach (var text in GuideLines)
        {
            guide.Cell(row++, 1).Value = text;
        }

        row++;
        guide.Cell(row, 1).Value = "Ví dụ";
        guide.Cell(row, 1).Style.Font.SetBold();
        row++;
        var exampleTop = row;
        string[][] example =
        [
            [GradeLabel, grades.Count > 0 ? grades[0] : "Lớp 5"],
            [FieldLabel, fields.Count > 0 ? fields[0] : "Toán"],
            [],
            Headers,
            ["1", "Ôn tập và bổ sung", "1", "Ôn tập số tự nhiên"],
            ["", "", "2", "Ôn tập các phép tính với số tự nhiên"],
            ["2", "Số thập phân", "1", "Khái niệm số thập phân"],
            ["3", "Hình học", "", ""]
        ];
        foreach (var cells in example)
        {
            for (var column = 0; column < cells.Length; column++)
            {
                guide.Cell(row, column + 1).Value = WorkbookText.Safe(cells[column]);
            }

            row++;
        }

        guide.Range(exampleTop, 1, exampleTop + 1, 1).Style.Font.SetBold();
        guide.Range(exampleTop + 3, 1, exampleTop + 3, Headers.Length).Style.Font.SetBold();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static CurriculumImportSheet Read(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            if (!workbook.Worksheets.TryGetWorksheet(DataSheet, out var sheet))
            {
                throw new CurriculumImportFormatException(
                    $"Tệp không đúng mẫu: không có sheet {DataSheet}. Vui lòng tải mẫu mới và điền lại.");
            }

            // The old flat template had one "Khối lớp" column per row, starting at A1.
            if (IsLabel(sheet.Cell(1, 1), GradeLabel))
            {
                throw new CurriculumImportFormatException("Tệp theo mẫu cũ. Vui lòng tải file mẫu mới và điền lại.");
            }

            if (!IsLabel(sheet.Cell(GradeRow, 1), GradeLabel) ||
                !IsLabel(sheet.Cell(FieldRow, 1), FieldLabel) ||
                Headers.Where((header, index) => !IsLabel(sheet.Cell(HeaderRow, index + 1), header)).Any())
            {
                throw new CurriculumImportFormatException(
                    "Tệp không đúng mẫu: đã đổi nhãn hoặc vị trí ô. Vui lòng tải mẫu mới và điền lại.");
            }

            var rows = new List<CurriculumImportRawRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? HeaderRow;
            for (var rowNumber = FirstDataRow; rowNumber <= lastRow; rowNumber++)
            {
                var cells = Enumerable.Range(1, Headers.Length)
                    .Select(column => Text(sheet.Cell(rowNumber, column)))
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

                // A lesson row with both chapter cells empty (or merged into the row above) belongs to the chapter
                // above. With only one of them empty nothing is filled, so the missing cell is reported.
                if (cells[0].Length == 0 && cells[1].Length == 0 && rows.Count > 0)
                {
                    cells[0] = rows[^1].ChapterNumber;
                    cells[1] = rows[^1].ChapterTitle;
                }

                rows.Add(new CurriculumImportRawRow(rowNumber, cells[0], cells[1], cells[2], cells[3]));
            }

            if (rows.Count == 0)
            {
                throw new CurriculumImportFormatException("Tệp không có dòng dữ liệu nào.");
            }

            return new CurriculumImportSheet(Text(sheet.Cell(GradeRow, 2)), Text(sheet.Cell(FieldRow, 2)), rows);
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

    // A bold label in column A and an input cell in column B whose dropdown lists one column of the hidden sheet.
    private static void AddChoice(
        IXLWorksheet data,
        int row,
        string label,
        IXLWorksheet lists,
        int listColumn,
        IReadOnlyList<string> values,
        string error)
    {
        data.Cell(row, 1).Value = label;
        data.Cell(row, 1).Style.Font.SetBold();
        var input = data.Cell(row, 2);
        input.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#eaf0fb"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetOutsideBorderColor(Accent);

        for (var index = 0; index < values.Count; index++)
        {
            lists.Cell(index + 1, listColumn).Value = WorkbookText.Safe(values[index]);
        }

        if (values.Count == 0)
        {
            return;
        }

        var validation = input.CreateDataValidation();
        validation.List(lists.Range(1, listColumn, values.Count, listColumn), true);
        validation.ErrorStyle = XLErrorStyle.Stop;
        validation.ErrorMessage = error;
    }

    private static bool IsLabel(IXLCell cell, string label) =>
        string.Equals(cell.GetString().Trim(), label, StringComparison.OrdinalIgnoreCase);

    private static string Text(IXLCell cell) => cell.GetFormattedString().Trim();

    private static readonly string[] GuideLines =
    [
        $"Nhập dữ liệu ở sheet {DataSheet}. Không đổi tên sheet, không đổi nhãn và vị trí các ô.",
        $"Chọn Khối lớp ở ô B{GradeRow} và Môn học ở ô B{FieldRow} trong danh sách. Mọi dòng trong tệp dùng khối lớp và môn học này.",
        "Mỗi tệp chỉ nhập cho một khối lớp và một môn học. Môn học khác thì dùng tệp khác.",
        $"Nhập bảng từ dòng {FirstDataRow}. Mỗi dòng của bảng là một bài học.",
        "Dòng tiếp theo của cùng chương có thể để trống Số chương và Tên chương (hoặc gộp ô): dòng đó thuộc chương ở dòng trên.",
        "Chương chưa có bài: để trống Số bài và Tên bài.",
        "Bắt buộc: Số chương, Tên chương. Số bài và Tên bài nhập cùng nhau hoặc cùng để trống.",
        "Số chương, Số bài là số nguyên từ 1 đến 999.",
        "Số và tên chương không được trùng trong cùng khối lớp và môn học; số và tên bài không được trùng trong cùng chương.",
        "Chương đã có trong hệ thống (cùng khối, môn học, số chương và tên chương) thì bài mới được thêm vào chương đó.",
        "Nếu có bất kỳ lỗi nào, hệ thống không lưu gì. Sửa tệp rồi tải lên lại.",
        $"Tối đa {MaxRows} dòng dữ liệu mỗi tệp. Chỉ nhận tệp .xlsx."
    ];
}
