using ClosedXML.Excel;
using Infrastructure.Exports;

namespace Infrastructure.Tests.Exports;

public sealed class StudentImportWorkbookTests
{
    private static readonly StudentImportClassOption[] Classes =
    [
        new("6A", "Lớp 6A"),
        new("6B", "Lớp 6B")
    ];

    [Fact]
    public void Template_HasTheDataSheetWithSixBoldHeadersInContractOrder()
    {
        var bytes = StudentImportWorkbook.CreateTemplate("2025-2026", Classes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(StudentImportWorkbook.DataSheet);
        Assert.Equal(
            new[]
            {
                "Mã học sinh (*)", "Họ và tên (*)", "Ngày sinh", "Giới tính",
                "Ngày nhập học (*)", "Mã lớp (*)"
            },
            Enumerable.Range(1, 6).Select(column => sheet.Cell(1, column).GetString()));
        Assert.True(sheet.Row(1).Style.Font.Bold);
        Assert.Equal(StudentImportWorkbook.Columns.Select(c => c.Header),
            Enumerable.Range(1, 6).Select(column => sheet.Cell(1, column).GetString()));
    }

    [Fact]
    public void Template_GuideSheetListsRealClassCodesAndTheAcademicYear()
    {
        var bytes = StudentImportWorkbook.CreateTemplate("2025-2026", Classes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var guide = workbook.Worksheet(StudentImportWorkbook.GuideSheet);
        var text = string.Join("\n", guide.CellsUsed().Select(cell => cell.GetString()));

        Assert.Contains("2025-2026", text);
        Assert.Contains("6A", text);
        Assert.Contains("Lớp 6B", text);
        Assert.Contains("dd/MM/yyyy", text);
    }

    [Fact]
    public void Template_EscapesFormulaTextComingFromClassNames()
    {
        var bytes = StudentImportWorkbook.CreateTemplate(
            "2025-2026", [new("=CMD()", "+SUM(A1)")]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var guide = workbook.Worksheet(StudentImportWorkbook.GuideSheet);
        var cells = guide.CellsUsed().Where(cell =>
            cell.GetString().Contains("CMD") || cell.GetString().Contains("SUM")).ToList();

        Assert.Equal(2, cells.Count);
        // ClosedXML turns the leading apostrophe into Excel's "quote prefix" flag, so the cell is
        // plain text that Excel will not evaluate.
        Assert.All(cells, cell =>
        {
            Assert.False(cell.HasFormula);
            Assert.Equal(XLDataType.Text, cell.DataType);
            Assert.True(cell.Style.IncludeQuotePrefix);
        });
    }

    [Fact]
    public void Read_RoundTripsAllSixFieldsAndKeepsSheetRowNumbers()
    {
        var bytes = Filled(sheet =>
        {
            Row(sheet, 2, "HS001", "Nguyễn Văn An", "15/03/2014", "NAM", "05/09/2025", "6A");
            Row(sheet, 4, "HS002", "Trần Thị Bình", "", "", "05/09/2025", "6B");
        });

        var rows = StudentImportWorkbook.Read(new MemoryStream(bytes));

        Assert.Equal(new[] { 2, 4 }, rows.Select(r => r.RowNumber));
        Assert.Equal(
            new StudentImportRawRow(2, "HS001", "Nguyễn Văn An", "15/03/2014", "NAM", "05/09/2025", "6A"),
            rows[0]);
        Assert.Equal("", rows[1].DateOfBirth);
        Assert.Equal("6B", rows[1].ClassCode);
    }

    [Fact]
    public void Read_TreatsARealDateCellAndTheTypedStringTheSame()
    {
        var bytes = Filled(sheet =>
        {
            Row(sheet, 2, "HS001", "An", "15/03/2014", "NAM", "05/09/2025", "6A");
            Row(sheet, 3, "HS002", "Bình", "", "NAM", "", "6A");
            sheet.Cell(3, 3).Value = new DateTime(2014, 3, 15);
            sheet.Cell(3, 5).Value = new DateTime(2025, 9, 5);
        });

        var rows = StudentImportWorkbook.Read(new MemoryStream(bytes));

        Assert.Equal(rows[0].DateOfBirth, rows[1].DateOfBirth);
        Assert.Equal(rows[0].AdmissionDate, rows[1].AdmissionDate);
    }

    [Fact]
    public void Read_SkipsFullyBlankRowsAndTrimsCells()
    {
        var bytes = Filled(sheet =>
        {
            Row(sheet, 2, "  HS001  ", "  An  ", "", "", "05/09/2025", "6A");
            Row(sheet, 3, "", "", "", "", "", "");
            Row(sheet, 4, "HS002", "Bình", "", "", "05/09/2025", "6A");
        });

        var rows = StudentImportWorkbook.Read(new MemoryStream(bytes));

        Assert.Equal(2, rows.Count);
        Assert.Equal("HS001", rows[0].Code);
        Assert.Equal("An", rows[0].FullName);
    }

    [Fact]
    public void Read_FallsBackToTheFirstSheetWhenTheDataSheetWasRenamed()
    {
        var bytes = Filled(sheet =>
        {
            sheet.Name = "Sheet1";
            Row(sheet, 2, "HS001", "An", "", "", "05/09/2025", "6A");
        });

        var rows = StudentImportWorkbook.Read(new MemoryStream(bytes));

        Assert.Equal("HS001", Assert.Single(rows).Code);
    }

    [Fact]
    public void Read_RejectsMoreThanTheRowLimit()
    {
        var bytes = Filled(sheet =>
        {
            for (var i = 0; i <= StudentImportWorkbook.MaxRows; i++)
            {
                Row(sheet, i + 2, $"HS{i}", "An", "", "", "05/09/2025", "6A");
            }
        });

        var failure = Assert.Throws<StudentImportFormatException>(
            () => StudentImportWorkbook.Read(new MemoryStream(bytes)));

        Assert.Contains(StudentImportWorkbook.MaxRows.ToString(), failure.Message);
    }

    [Fact]
    public void Read_AcceptsExactlyTheRowLimit()
    {
        var bytes = Filled(sheet =>
        {
            for (var i = 0; i < StudentImportWorkbook.MaxRows; i++)
            {
                Row(sheet, i + 2, $"HS{i}", "An", "", "", "05/09/2025", "6A");
            }
        });

        var rows = StudentImportWorkbook.Read(new MemoryStream(bytes));

        Assert.Equal(StudentImportWorkbook.MaxRows, rows.Count);
    }

    [Fact]
    public void Read_RejectsAnEmptyTemplateAndNonWorkbookBytes()
    {
        var emptyTemplate = StudentImportWorkbook.CreateTemplate("2025-2026", Classes);

        Assert.Throws<StudentImportFormatException>(
            () => StudentImportWorkbook.Read(new MemoryStream(emptyTemplate)));
        Assert.Throws<StudentImportFormatException>(
            () => StudentImportWorkbook.Read(new MemoryStream("not,an,xlsx\n1,2,3"u8.ToArray())));
        Assert.Throws<StudentImportFormatException>(
            () => StudentImportWorkbook.Read(new MemoryStream([])));
    }

    // Builds a workbook shaped like the template, then lets the test fill the data sheet.
    private static byte[] Filled(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook(new MemoryStream(
            StudentImportWorkbook.CreateTemplate("2025-2026", Classes)));
        fill(workbook.Worksheet(StudentImportWorkbook.DataSheet));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Row(
        IXLWorksheet sheet, int row, string code, string name, string dob,
        string gender, string admission, string classCode)
    {
        sheet.Cell(row, 1).SetValue(code);
        sheet.Cell(row, 2).SetValue(name);
        sheet.Cell(row, 3).SetValue(dob);
        sheet.Cell(row, 4).SetValue(gender);
        sheet.Cell(row, 5).SetValue(admission);
        sheet.Cell(row, 6).SetValue(classCode);
    }
}
