using ClosedXML.Excel;
using Infrastructure.Exports;

namespace Infrastructure.Tests.Exports;

public sealed class MatrixWorkbookExporterTests
{
    [Fact]
    public void WorkbookContainsDerivedTotalsAndEscapesFormulaText()
    {
        var exporter = new ClosedXmlMatrixWorkbookExporter();
        var bytes = exporter.Create(new MatrixWorkbookModel(
            "=SUM(A1)",
            "APPROVED",
            null,
            "Toán - Lớp 5",
            "Học kỳ I",
            2,
            1.5m,
            new[]
            {
                new MatrixWorkbookRow("Bài 1", "+LEVEL", "@TYPE", 2, 100m, 1.5m)
            }));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Matrix");

        Assert.Equal("=SUM(A1)", sheet.Cell("B2").GetString());
        Assert.True(string.IsNullOrEmpty(sheet.Cell("B2").FormulaA1));
        Assert.Equal((uint)2, sheet.Cell("B7").GetValue<uint>());
        Assert.Equal(1.5m, sheet.Cell("B8").GetValue<decimal>());
        Assert.Equal("Bài 1", sheet.Cell("A11").GetString());
        Assert.Equal("+LEVEL", sheet.Cell("B11").GetString());
        Assert.Equal("@TYPE", sheet.Cell("C11").GetString());
        Assert.True(string.IsNullOrEmpty(sheet.Cell("B11").FormulaA1));
        Assert.True(string.IsNullOrEmpty(sheet.Cell("C11").FormulaA1));
    }
}
