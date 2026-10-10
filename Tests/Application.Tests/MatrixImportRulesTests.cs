using Application.Services.Implement;
using ClosedXML.Excel;
using Infrastructure.Exports;
using Infrastructure.Models;
using Xunit;

namespace Application.Tests;

public sealed class MatrixImportRulesTests
{
    private static readonly MatrixLessonOption[] Lessons =
    [
        new(1, 10, 11, "Phân số", 1, "1", "I", "Số và phép tính"),
        new(2, 10, 12, "Phân số", 2, "1", "II", "Ôn tập"),
        new(3, 10, 11, "Hỗn số", 3, "2", "I", "Số và phép tính")
    ];

    [Fact]
    public void ValidFile_GroupsLevelsAndPreservesMetadata()
    {
        var result = Read(["Hỗn số", "nhan biet", "Trắc nghiệm", "2", "20%"],
            ["Hỗn số", "THONG_HIEU", "Trắc nghiệm", "3", "33,33"]);
        Assert.True(result.CanImport);
        Assert.Equal(1, result.LessonCount);
        Assert.Equal(2, result.FilledLines);
        Assert.Equal("Toán", result.Name);
        Assert.Equal(10, result.TotalScore);
        Assert.Equal(33.33m, result.Details[1].Percentage);
    }

    [Theory]
    [InlineData("", "Nhận biết", "Trắc nghiệm", "2", "20")]
    [InlineData("Sai chương / Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20")]
    [InlineData("Phân số", "Nhận biết", "Trắc nghiệm", "2", "20")]
    [InlineData("Hỗn số", "Sai mức", "Trắc nghiệm", "2", "20")]
    [InlineData("Hỗn số", "Nhận biết", "Tự luận", "2", "20")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "1.5", "20")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "0", "20")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "4294967296", "20")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "0")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "101")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20.123")]
    [InlineData("Hỗn số", "Nhận biết", "Trắc nghiệm", "abc", "20")]
    public void AnyInvalidRow_BlocksWholeFile(string lesson, string level, string type, string count, string percentage)
    {
        var result = Read(["Hỗn số", "Vận dụng", "Trắc nghiệm", "1", "10"],
            [lesson, level, type, count, percentage]);
        Assert.False(result.CanImport);
        Assert.Empty(result.Details);
        Assert.Contains(result.Errors, error => error.RowNumber == 5);
    }

    [Fact]
    public void DuplicateCell_BlocksWholeFile()
    {
        var result = Read(["Hỗn số", "Nhận biết", "", "2", "20"], ["Hỗn số", "NHAN_BIET", "", "1", "10"]);
        Assert.False(result.CanImport);
        Assert.Contains(result.Errors, error => error.RowNumber == 5 && error.Message.Contains("dòng 4"));
    }

    [Fact]
    public void BlankTemplate_IsNotImportable()
    {
        var result = Read(["Hỗn số", "Nhận biết", "Trắc nghiệm", "", ""]);
        Assert.False(result.CanImport);
        Assert.Empty(result.Details);
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("Bài 1. Phân số — Chương I. Số và phép tính", 1)]
    [InlineData("Số và phép tính / Phân số", 1)]
    [InlineData("Bài 1. Phân số — Chương II. Ôn tập", 2)]
    public void QualifiedLesson_ResolvesCorrectChapter(string label, int id)
    {
        var result = Read([label, "Nhận biết", "Trắc nghiệm", "1", "100"]);
        Assert.True(result.CanImport);
        Assert.Equal((ulong)id, Assert.Single(result.Details).LessonId);
    }

    [Fact]
    public void Formula_IsRejectedInsteadOfUsingCachedValue()
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(4, 4).FormulaA1 = "1+1";
        Assert.Throws<MatrixImportFormatException>(() => MatrixImportWorkbook.Read(Bytes(book)));
    }

    [Fact]
    public void InvalidTotalScore_BlocksWholeFile()
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(2, 2).Value = "10.5";
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
        Assert.False(result.CanImport);
        Assert.Contains(result.Errors, error => error.RowNumber == 2);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    public void ExcelPercentageNumberFormat_PreservesActualPercentage(int format)
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(4, 5).Value = 0.3333;
        book.Worksheet(1).Cell(4, 5).Style.NumberFormat.NumberFormatId = format;
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
        Assert.True(result.CanImport);
        Assert.Equal(33.33m, Assert.Single(result.Details).Percentage);
    }

    [Fact]
    public void PercentageFormattingOnCount_IsNotAcceptedAsInteger()
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(4, 4).Value = 0.02;
        book.Worksheet(1).Cell(4, 4).Style.NumberFormat.NumberFormatId = 9;
        Assert.False(MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10).CanImport);
    }

    [Fact]
    public void ExcessiveRowAndColumnBounds_AreRejected()
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(MatrixImportWorkbook.MaxRows + 1, 1).Value = "x";
        Assert.Throws<MatrixImportFormatException>(() => MatrixImportWorkbook.Read(Bytes(book)));
        using var wideBook = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        wideBook.Worksheet(1).Cell(1, 33).Value = "x";
        Assert.Throws<MatrixImportFormatException>(() => MatrixImportWorkbook.Read(Bytes(wideBook)));
    }

    [Fact]
    public void ExportedWorkbook_RoundTripsToImport()
    {
        var bytes = new ClosedXmlMatrixWorkbookExporter().Create(new MatrixWorkbookModel("Toán", "APPROVED", null,
            "Toán 5", "HK I", 2, 10, [new("Số và phép tính / Phân số", "NHAN_BIET", "MULTIPLE_CHOICE", 2, 100, 10)]));
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(bytes), Lessons, 10);
        Assert.True(result.CanImport);
        Assert.Equal(1UL, Assert.Single(result.Details).LessonId);
    }

    [Theory]
    [InlineData("Loại câu hỏi", "Tự luận")]
    [InlineData("Tỷ lệ điểm", "0")]
    public void AmbiguousOptionalOrPercentageHeaders_BlockWholeFile(string header, string value)
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "100"]);
        book.Worksheet(1).Cell(3, 6).Value = header;
        book.Worksheet(1).Cell(4, 6).Value = value;
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
        Assert.False(result.CanImport);
        Assert.Empty(result.Details);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ArchiveLimits_AreCheckedBeforeOpeningWorkbook(bool tooManyEntries)
    {
        using var bytes = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(bytes, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            if (tooManyEntries)
            {
                for (var i = 0; i < 1001; i++) zip.CreateEntry($"entry-{i}");
            }
            else
            {
                using var stream = zip.CreateEntry("expanded", System.IO.Compression.CompressionLevel.SmallestSize).Open();
                var chunk = new byte[32768];
                for (var i = 0; i < 1601; i++) stream.Write(chunk);
            }
        }
        var error = Assert.Throws<MatrixImportFormatException>(() => MatrixImportWorkbook.Read(bytes.ToArray()));
        Assert.Contains("giải nén", error.Message);
    }

    [Theory]
    [InlineData("0\"%\"")]
    [InlineData("0\\%")]
    public void LiteralPercentageFormat_DoesNotScaleValue(string format)
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "20"]);
        book.Worksheet(1).Cell(4, 5).Value = 20;
        book.Worksheet(1).Cell(4, 5).Style.NumberFormat.Format = format;
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
        Assert.True(result.CanImport);
        Assert.Equal(20m, Assert.Single(result.Details).Percentage);
    }

    [Fact]
    public void TemplateIdentifier_DisambiguatesSameChapterAndLessonLabelsAcrossFields()
    {
        MatrixLessonOption[] lessons = [Lessons[0], Lessons[0] with { Id = 4, ChapterId = 99 }];
        using var book = Book([MatrixImportWorkbook.LessonLabel(Lessons[0]), "Nhận biết", "Trắc nghiệm", "2", "100", "4"]);
        book.Worksheet(1).Cell(3, 6).Value = "ID bài học";
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), lessons, 10);
        Assert.True(result.CanImport);
        Assert.Equal(4UL, Assert.Single(result.Details).LessonId);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("abc")]
    [InlineData("1")]
    public void WrongTemplateIdentifier_DoesNotOverrideLessonNameOrScope(string id)
    {
        using var book = Book(["Hỗn số", "Nhận biết", "Trắc nghiệm", "2", "100", id]);
        book.Worksheet(1).Cell(3, 6).Value = "ID bài học";
        var result = MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
        Assert.False(result.CanImport);
        Assert.Empty(result.Details);
    }

    private static Application.DTOs.MatrixImportPreview Read(params string[][] rows)
    {
        using var book = Book(rows);
        return MatrixImportRules.Evaluate(MatrixImportWorkbook.Read(Bytes(book)), Lessons, 10);
    }

    private static XLWorkbook Book(params string[][] rows)
    {
        var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Matrix");
        sheet.Cell(1, 1).Value = "Tên ma trận";
        sheet.Cell(1, 2).Value = "Toán";
        sheet.Cell(2, 1).Value = "Tổng điểm";
        sheet.Cell(2, 2).Value = 10;
        var headers = new[] { "Bài học", "Mức nhận thức", "Loại câu hỏi", "Số câu", "Tỷ lệ %" };
        for (var col = 0; col < headers.Length; col++) sheet.Cell(3, col + 1).Value = headers[col];
        for (var row = 0; row < rows.Length; row++)
            for (var col = 0; col < rows[row].Length; col++) sheet.Cell(row + 4, col + 1).Value = rows[row][col];
        return book;
    }

    private static byte[] Bytes(XLWorkbook book)
    {
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
