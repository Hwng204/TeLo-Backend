using ClosedXML.Excel;

namespace Infrastructure.Exports;

public sealed record StudentImportClassOption(string Code, string Name);

// One sheet row exactly as typed. Dates are normalised to dd/MM/yyyy text so a real Excel date
// cell and a typed string look the same to the validator.
public sealed record StudentImportRawRow(
    int RowNumber,
    string Code,
    string FullName,
    string DateOfBirth,
    string Gender,
    string AdmissionDate,
    string ClassCode);

// The file cannot be read as a student list (not an .xlsx, empty, or over the row limit).
public sealed class StudentImportFormatException(string message) : Exception(message);

// Builds the download template and reads a filled-in copy back. Stateless, so it is a static
// class: nothing about it needs substituting.
public static class StudentImportWorkbook
{
    public const int MaxRows = 1000;
    public const string DataSheet = "HocSinh";
    public const string GuideSheet = "HuongDan";
    public const string DateFormat = "dd/MM/yyyy";

    public static readonly string[] Genders = ["NAM", "NU", "KHAC"];

    // Column order is the contract with Read: A..F.
    public static readonly StudentImportColumn[] Columns =
    [
        new("code", "Mã học sinh (*)", true, "Chữ/số, tối đa 64 ký tự, không trùng", "HS2025001"),
        new("fullName", "Họ và tên (*)", true, "Tối đa 255 ký tự", "Nguyễn Văn An"),
        new("dateOfBirth", "Ngày sinh", false, "dd/MM/yyyy", "15/03/2014"),
        new("gender", "Giới tính", false, "NAM, NU hoặc KHAC", "NAM"),
        new("admissionDate", "Ngày nhập học (*)", true, "dd/MM/yyyy", "05/09/2025"),
        new("classCode", "Mã lớp (*)", true, "Chọn trong sheet HuongDan", "6A")
    ];

    public static byte[] CreateTemplate(
        string academicYearName,
        IReadOnlyList<StudentImportClassOption> classes)
    {
        using var workbook = new XLWorkbook();

        var data = workbook.Worksheets.Add(DataSheet);
        for (var column = 0; column < Columns.Length; column++)
        {
            data.Cell(1, column + 1).Value = Columns[column].Header;
        }

        data.Row(1).Style.Font.SetBold();
        // Text format keeps leading zeros of a student code and stops Excel guessing dates.
        data.Column(1).Style.NumberFormat.Format = "@";
        data.Column(6).Style.NumberFormat.Format = "@";
        data.Column(3).Style.NumberFormat.Format = DateFormat;
        data.Column(5).Style.NumberFormat.Format = DateFormat;
        data.SheetView.FreezeRows(1);
        data.Columns().AdjustToContents();

        var guide = workbook.Worksheets.Add(GuideSheet);
        var row = 1;
        guide.Cell(row, 1).Value = "HƯỚNG DẪN NHẬP DANH SÁCH HỌC SINH";
        guide.Cell(row, 1).Style.Font.SetBold();
        row += 2;
        foreach (var line in GuideLines(academicYearName))
        {
            guide.Cell(row++, 1).Value = line;
        }

        row++;
        guide.Cell(row, 1).Value = "Cột";
        guide.Cell(row, 2).Value = "Bắt buộc";
        guide.Cell(row, 3).Value = "Định dạng";
        guide.Cell(row, 4).Value = "Ví dụ";
        guide.Range(row, 1, row, 4).Style.Font.SetBold();
        row++;
        foreach (var column in Columns)
        {
            guide.Cell(row, 1).Value = column.Header;
            guide.Cell(row, 2).Value = column.Required ? "Có" : "Không";
            guide.Cell(row, 3).Value = column.Format;
            guide.Cell(row, 4).Value = column.Example;
            row++;
        }

        row += 2;
        guide.Cell(row, 1).Value = "Mã lớp";
        guide.Cell(row, 2).Value = "Tên lớp";
        guide.Range(row, 1, row, 2).Style.Font.SetBold();
        row++;
        foreach (var option in classes)
        {
            // Class names come from user input, so guard them like any other exported text.
            guide.Cell(row, 1).Value = WorkbookText.Safe(option.Code);
            guide.Cell(row, 2).Value = WorkbookText.Safe(option.Name);
            row++;
        }

        guide.Column(1).AdjustToContents();
        guide.Column(2).AdjustToContents();
        guide.Column(3).AdjustToContents();
        guide.Column(4).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<StudentImportRawRow> Read(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.TryGetWorksheet(DataSheet, out var named)
                ? named
                : workbook.Worksheets.FirstOrDefault()
                    ?? throw new StudentImportFormatException("File không có sheet dữ liệu.");

            var rows = new List<StudentImportRawRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var raw = new StudentImportRawRow(
                    rowNumber,
                    CellText(sheet.Cell(rowNumber, 1)),
                    CellText(sheet.Cell(rowNumber, 2)),
                    CellText(sheet.Cell(rowNumber, 3)),
                    CellText(sheet.Cell(rowNumber, 4)),
                    CellText(sheet.Cell(rowNumber, 5)),
                    CellText(sheet.Cell(rowNumber, 6)));

                // Trailing formatting leaves empty rows behind; they are not student rows.
                if (raw.Code.Length + raw.FullName.Length + raw.DateOfBirth.Length +
                    raw.Gender.Length + raw.AdmissionDate.Length + raw.ClassCode.Length == 0)
                {
                    continue;
                }

                if (rows.Count >= MaxRows)
                {
                    throw new StudentImportFormatException(
                        $"File có nhiều hơn {MaxRows} dòng dữ liệu. Vui lòng chia nhỏ file.");
                }

                rows.Add(raw);
            }

            if (rows.Count == 0)
            {
                throw new StudentImportFormatException("File không có dòng dữ liệu nào.");
            }

            return rows;
        }
        catch (StudentImportFormatException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // ClosedXML/OpenXML throw a variety of types for a corrupt or non-xlsx file.
            throw new StudentImportFormatException(
                "Không đọc được file. Vui lòng dùng file Excel .xlsx theo mẫu.")
            {
                Data = { ["cause"] = exception.GetType().Name }
            };
        }
    }

    private static string CellText(IXLCell cell) =>
        cell.DataType == XLDataType.DateTime
            ? cell.GetDateTime().ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture)
            : cell.GetString().Trim();

    private static IEnumerable<string> GuideLines(string academicYearName) =>
    [
        $"Năm học áp dụng: {WorkbookText.Safe(academicYearName)}",
        "Nhập dữ liệu ở sheet HocSinh, mỗi học sinh một dòng, bắt đầu từ dòng 2. Không đổi thứ tự cột.",
        "Các cột có dấu (*) là bắt buộc.",
        $"Ngày nhập theo định dạng {DateFormat}, ví dụ 15/03/2014.",
        "Mã học sinh đã tồn tại trong hệ thống sẽ báo lỗi ở dòng đó, không ghi đè hồ sơ cũ.",
        "Mã lớp phải là một mã trong bảng ở cuối trang này.",
        $"Tối đa {MaxRows} dòng mỗi file. Chỉ nhận file .xlsx."
    ];
}

public sealed record StudentImportColumn(
    string Key,
    string Header,
    bool Required,
    string Format,
    string Example);
