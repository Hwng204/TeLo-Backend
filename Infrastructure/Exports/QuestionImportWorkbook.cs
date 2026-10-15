using ClosedXML.Excel;
using System.Globalization;
using System.IO.Compression;

namespace Infrastructure.Exports;

public sealed record QuestionWorkbookTarget(ulong Id, string Lesson, string Level, uint Required);
public sealed record QuestionWorkbookRow(int Row, ulong TargetId, string Lesson, string Level, string Content, string[] Options, string? Answer, string? Explanation, bool PrefilledContext = false);

public static class QuestionImportWorkbook
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxQuestions = 5000;
    public static readonly string[] Headers = ["Nội dung câu hỏi *", "A", "B", "C", "D", "Đáp án đúng *", "Giải thích (nếu có)", "Mã yêu cầu"];
    private static readonly string[] LegacyHeaders = ["Mã yêu cầu", "Bài học", "Mức nhận thức", "Nội dung", "A", "B", "C", "D", "Đáp án đúng", "Giải thích"];

    public static byte[] Template(IReadOnlyList<QuestionWorkbookTarget> targets)
    {
        if (targets.Sum(t => (long)t.Required) > MaxQuestions)
            throw new InvalidDataException("Một tệp tối đa 5.000 câu hỏi. Vui lòng chia nhỏ nhiệm vụ.");
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Câu hỏi");
        sheet.Range("A1:G1").Merge().Value = "NHẬP CÂU HỎI";
        sheet.Range("A2:G2").Merge().Value = "Mỗi dòng một câu. Nhập ít nhất 2 lựa chọn trong A–D; ô không dùng để trống. Chọn đáp án đúng ở cột F. Dòng chưa làm để trống.";
        for (var i = 0; i < Headers.Length; i++) sheet.Cell(3, i + 1).Value = Headers[i];
        var row = 4;
        foreach (var target in targets.Where(t => t.Required > 0))
        {
            sheet.Range(row, 1, row, 7).Merge().Value = WorkbookText.Safe($"{target.Lesson} — {target.Level} — Cần thêm {target.Required} câu");
            sheet.Cell(row, 8).Value = "Nhóm:" + target.Id.ToString(CultureInfo.InvariantCulture);
            sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#eaf0fb");
            sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
            sheet.Row(row++).Height = 30;
            for (var count = 0; count < target.Required; count++, row++)
            {
                sheet.Cell(row, 8).Value = target.Id.ToString(CultureInfo.InvariantCulture);
                sheet.Range(row, 1, row, 7).Style.NumberFormat.Format = "@";
                sheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff9e8");
                sheet.Cell(row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#f6f7f9");
                var validation = sheet.Cell(row, 6).CreateDataValidation();
                validation.List("\"A,B,C,D\"", true);
                validation.ErrorTitle = "Chọn đáp án đúng";
                validation.ErrorMessage = "Chọn A, B, C hoặc D có nội dung ở dòng này.";
                validation.ShowErrorMessage = true;
                sheet.Row(row).Height = 60;
            }
        }
        StyleInput(sheet, row - 1);
        sheet.Column(8).Hide();
        sheet.SheetView.FreezeRows(3); sheet.SheetView.FreezeColumns(1);
        return Save(workbook);
    }

    private static void StyleInput(IXLWorksheet sheet, int lastRow)
    {
        var area = sheet.Range(1, 1, lastRow, 7);
        area.Style.Font.FontName = "Arial"; area.Style.Font.FontSize = 11;
        area.Style.Font.FontColor = XLColor.FromHtml("#25334a");
        area.Style.Alignment.WrapText = true; area.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.ShowGridLines = false;
        sheet.Column(1).Width = 44; sheet.Columns(2, 5).Width = 18; sheet.Column(6).Width = 14; sheet.Column(7).Width = 28;
        sheet.Range("A1:G1").Style.Font.FontSize = 16; sheet.Range("A1:G1").Style.Font.Bold = true;
        sheet.Row(1).Height = 30; sheet.Row(2).Height = 36; sheet.Row(3).Height = 36;
        sheet.Range("A3:G3").Style.Fill.BackgroundColor = XLColor.FromHtml("#395e9b");
        sheet.Range("A3:G3").Style.Font.FontColor = XLColor.White; sheet.Range("A3:G3").Style.Font.Bold = true;
        sheet.Range("A3:G3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.PrintAreas.Add(1, 1, lastRow, 7);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    public static byte[] Guide()
    {
        using var workbook = new XLWorkbook();
        var guide = workbook.AddWorksheet("Hướng dẫn");
        string[][] lines =
        [
            ["HƯỚNG DẪN NHẬP CÂU HỎI", ""],
            ["1. Tải mẫu", "Trong gói câu hỏi, chọn Nhập Excel → Tải file mẫu. Mẫu đã chia nhóm bài học, mức nhận thức và số dòng còn thiếu."],
            ["2. Điền câu hỏi", "Mỗi dòng một câu. Điền nội dung, ít nhất 2 lựa chọn trong A–D, rồi chọn chữ cái đáp án đúng ở cột F. Ô lựa chọn không dùng để trống. Giải thích có thể bỏ qua."],
            ["3. Lưu và chọn tệp", "Lưu tệp .xlsx. Quay lại Nhập Excel, chọn tệp vừa lưu. Đọc kết quả kiểm tra; nếu có lỗi, sửa đúng dòng được báo rồi chọn lại tệp."],
            ["4. Xác nhận nhập", "Khi tệp hợp lệ, chọn Xác nhận nhập. Câu hỏi được bổ sung vào gói. Khi đủ câu theo từng bài và mức, chọn Gửi gói câu hỏi."],
            ["Xem ví dụ", "Mở trang Ví dụ để xem các câu đã điền. Đây là ví dụ cách nhập; hãy soạn nội dung đúng môn và bài được giao."],
            ["Lựa chọn để trống", "Giữ đủ bốn ô A–D. Có thể bỏ trống bất kỳ ô nào không dùng; không cần nhập dấu gạch ngang. Chỉ chọn đáp án đúng trong các ô đã điền."],
            ["Mẫu đề yêu cầu 4 lựa chọn", "Nếu tổ trưởng hoặc mẫu đề yêu cầu đủ bốn lựa chọn, điền cả A, B, C và D."],
            ["Dòng chưa làm", "Có thể chỉ nhập một phần câu hỏi. Dòng chưa làm để trống hoàn toàn và tiếp tục nhập ở lần sau."],
            ["Giữ đúng nhóm bài", "Điền dưới tiêu đề nhóm được tạo sẵn. Không đổi tiêu đề cột, không di chuyển câu sang nhóm khác và không dùng công thức. Tệp tối đa 5 MB."],
            ["Lỗi thường gặp", "Thiếu nội dung; ít hơn 2 lựa chọn; lựa chọn trùng nhau; đáp án đúng trỏ đến ô trống; số câu vượt nhóm được giao."],
            ["File này dùng để xem", "Không tải file hướng dẫn lên hệ thống. Dùng file mẫu tải từ chính gói câu hỏi để nhập dữ liệu."]
        ];
        for (var row = 0; row < lines.Length; row++)
            for (var col = 0; col < 2; col++) guide.Cell(row + 1, col + 1).Value = lines[row][col];
        guide.Range("A1:B1").Merge(); guide.Column(1).Width = 30; guide.Column(2).Width = 100;
        guide.Range(1, 1, lines.Length, 2).Style.Font.FontName = "Arial";
        guide.Range(1, 1, lines.Length, 2).Style.Font.FontSize = 11;
        guide.Range(1, 1, lines.Length, 2).Style.Alignment.WrapText = true;
        guide.Range(1, 1, lines.Length, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        guide.Range("A1:B1").Style.Font.FontSize = 16; guide.Column(1).Style.Font.Bold = true;
        guide.Row(1).Height = 30; guide.Rows(2, lines.Length).Height = 54;
        guide.ShowGridLines = false;
        var example = workbook.AddWorksheet("Ví dụ");
        example.Range("A1:G1").Merge().Value = "VÍ DỤ ĐIỀN CÂU HỎI";
        example.Range("A2:G2").Merge().Value = "Tham khảo cách điền, rồi nhập câu hỏi của thầy/cô vào file mẫu. Ô lựa chọn không dùng để trống.";
        for (var col = 0; col < 7; col++) example.Cell(3, col + 1).Value = Headers[col];
        string[][] examples =
        [
            ["2 + 3 = ?", "4", "5", "", "", "B", "2 + 3 = 5."],
            ["Phân số nào bằng 1/2?", "2/4", "", "1/3", "", "A", "Nhân cả tử số và mẫu số của 1/2 với 2."],
            ["Số nào lớn nhất?", "3", "7", "5", "", "B", "7 lớn hơn 3 và 5."],
            ["2,5 − 0,8 = ?", "1,5", "1,7", "2,3", "3,3", "B", "2,5 − 0,8 = 1,7."]
        ];
        for (var row = 0; row < examples.Length; row++)
            for (var col = 0; col < 7; col++) example.Cell(row + 4, col + 1).Value = examples[row][col];
        StyleInput(example, 7); example.Rows(4, 7).Height = 60;
        return Save(workbook);
    }

    public static QuestionWorkbookRow[] Read(string filename, byte[] bytes)
    {
        if (!filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || bytes.Length is 0 or > MaxBytes) throw new InvalidDataException("Chọn tệp .xlsx không rỗng, tối đa 5 MB.");
        using var stream = new MemoryStream(bytes);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            if (zip.Entries.Count > 200 || zip.Entries.Sum(x => x.Length) > 50 * 1024 * 1024 || zip.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Tệp Excel vượt giới hạn hoặc chứa nội dung không được hỗ trợ.");
        }
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        if (!workbook.TryGetWorksheet("Câu hỏi", out var sheet))
            throw new InvalidDataException(workbook.TryGetWorksheet("Hướng dẫn", out _) ? "Đây là file hướng dẫn. Hãy chọn file mẫu đã điền câu hỏi." : "Thiếu trang Câu hỏi. Vui lòng sử dụng tệp mẫu.");
        bool Matches(int row, string[] headers) => headers.Select((h, i) => !sheet.Cell(row, i + 1).HasFormula && sheet.Cell(row, i + 1).GetString().Trim() == h).All(x => x);
        var simple = Matches(3, Headers);
        if (!simple && !Matches(1, LegacyHeaders)) throw new InvalidDataException("Các cột không khớp tệp mẫu. Tải lại mẫu trong gói câu hỏi.");
        var columns = simple ? 8 : 10;
        var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
        if (last > (simple ? MaxQuestions * 2 + 3 : MaxQuestions + 1) || (sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0) > columns)
            throw new InvalidDataException("Tệp vượt giới hạn dòng hoặc cột của file mẫu.");
        var result = new List<QuestionWorkbookRow>();
        ulong groupId = 0;
        for (var row = simple ? 4 : 2; row <= last; row++)
        {
            var cells = Enumerable.Range(1, columns).Select(col => sheet.Cell(row, col)).ToArray();
            if (cells.Any(x => x.HasFormula || x.GetString().Length > 10000)) throw new InvalidDataException($"Dòng {row}: không dùng công thức; nội dung mỗi ô tối đa 10.000 ký tự.");
            var values = cells.Select(x => x.GetString().Trim()).ToArray();
            if (simple && values[7].StartsWith("Nhóm:", StringComparison.Ordinal))
            {
                if (!ulong.TryParse(values[7][5..], out groupId) || groupId == 0 || values.Skip(1).Take(6).Any(x => x.Length > 0))
                    throw new InvalidDataException($"Dòng {row}: tiêu đề nhóm đã thay đổi. Tải lại file mẫu.");
                continue;
            }
            if (simple ? values.Take(7).All(x => x.Length == 0) : values.Skip(3).All(x => x.Length == 0)) continue;
            if (simple)
            {
                if (groupId == 0 || !ulong.TryParse(values[7], out var id) || id != groupId)
                    throw new InvalidDataException($"Dòng {row}: câu hỏi không khớp nhóm bài. Điền vào các dòng có sẵn trong file mẫu.");
                result.Add(new(row, id, "", "", values[0], values[1..5], values[5].Length == 0 ? null : values[5].ToUpperInvariant(), values[6], true));
            }
            else
            {
                ulong.TryParse(values[0], out var id);
                result.Add(new(row, id, values[1], values[2], values[3], values[4..8], values[8].Length == 0 ? null : values[8].ToUpperInvariant(), values[9]));
            }
            if (result.Count > MaxQuestions) throw new InvalidDataException("Tệp tối đa 5.000 câu hỏi.");
        }
        return result.ToArray();
    }
}
