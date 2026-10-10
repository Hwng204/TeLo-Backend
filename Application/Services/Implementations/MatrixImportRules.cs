using System.Globalization;
using System.Text;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;
using Infrastructure.Models;

namespace Application.Services.Implement;

public static class MatrixImportRules
{
    private static string Fold(string value)
    {
        var chars = value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => c == 'đ' || c == 'Đ' ? 'd' : char.ToLowerInvariant(c)).ToArray();
        return string.Join(' ', new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static MatrixImportPreview Evaluate(MatrixImportSheet sheet, IReadOnlyList<MatrixLessonOption> lessons, int defaultScore)
    {
        var errors = new List<MatrixImportError>();
        var details = new List<MatrixDetailRequest>();
        string? name = null;
        var score = defaultScore;
        var filled = 0;
        var headerIndex = -1;
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var cells = sheet.Rows[i].Select(Fold).ToArray();
            if (cells.Contains("bai hoc") && cells.Contains("muc nhan thuc")) { headerIndex = i; break; }
        }
        if (headerIndex < 0)
        {
            errors.Add(new(null, "Không tìm thấy dòng tiêu đề Bài học | Mức nhận thức | Số câu | Tỷ lệ %. Vui lòng dùng tệp mẫu hoặc tệp Xuất Excel."));
            return Result();
        }
        var headers = sheet.Rows[headerIndex].Select(Fold).ToArray();
        int Column(string title) => Array.IndexOf(headers, title);
        var lessonColumn = Column("bai hoc");
        var levelColumn = Column("muc nhan thuc");
        var countColumn = Column("so cau");
        var percentColumn = Array.FindIndex(headers, h => h.StartsWith("ty le"));
        var typeColumn = Column("loai cau hoi");
        var idColumn = Column("id bai hoc");
        if (countColumn < 0 || percentColumn < 0 ||
            headers.Count(h => h.StartsWith("ty le")) != 1 || headers.Count(h => h == "loai cau hoi") > 1 ||
            headers.Count(h => h == "id bai hoc") > 1 ||
            new[] { lessonColumn, levelColumn, countColumn, percentColumn }.Any(col => headers.Count(h => h == headers[col]) > 1))
        {
            errors.Add(new(headerIndex + 1, "Tiêu đề cột bị thiếu hoặc trùng. Vui lòng dùng tệp mẫu."));
            return Result();
        }
        for (var i = 0; i < headerIndex; i++)
        {
            var cells = sheet.Rows[i];
            var label = Fold(cells.ElementAtOrDefault(0) ?? "");
            var value = cells.ElementAtOrDefault(1) ?? "";
            if (label == "ten ma tran" && value.Length > 0)
            {
                name = value;
                if (name.Length > 255) errors.Add(new(i + 1, "Tên ma trận tối đa 255 ký tự."));
            }
            if (label == "tong diem")
            {
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0) score = parsed;
                else errors.Add(new(i + 1, "Tổng điểm phải là số nguyên dương."));
            }
        }

        var byName = new Dictionary<string, List<MatrixLessonOption>>();
        foreach (var lesson in lessons)
            foreach (var label in new[] { MatrixImportWorkbook.LessonLabel(lesson), $"{lesson.ChapterTitle} / {lesson.Title}", lesson.Title })
            {
                var key = Fold(label);
                if (!byName.TryGetValue(key, out var matches)) byName[key] = matches = [];
                if (!matches.Contains(lesson)) matches.Add(lesson);
            }
        var levels = MatrixCognitiveLevels.All.SelectMany(l => new[] { (Fold(l.Label), l.Code), (Fold(l.Code), l.Code), (Fold(l.Code.Replace('_', ' ')), l.Code) })
            .GroupBy(l => l.Item1).ToDictionary(g => g.Key, g => g.First().Code);
        var origins = new Dictionary<(ulong, string), int>();
        for (var i = headerIndex + 1; i < sheet.Rows.Count; i++)
        {
            var cells = sheet.Rows[i];
            string Value(int col) => col < 0 ? "" : cells.ElementAtOrDefault(col) ?? "";
            var countText = Value(countColumn);
            var percentText = Value(percentColumn);
            if (countText.Length == 0 && percentText.Length == 0) continue;
            filled++;
            var row = i + 1;
            var before = errors.Count;
            var title = Value(lessonColumn);
            byName.TryGetValue(Fold(title), out var matches);
            var idText = Value(idColumn);
            if (idText.Length > 0)
            {
                matches = ulong.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var lessonId)
                    ? matches?.Where(l => l.Id == lessonId).ToList()
                    : [];
            }
            if (matches is null || matches.Count == 0) errors.Add(new(row, $"Không tìm thấy bài học \"{title}\" trong chương trình đã chọn."));
            else if (matches.Count > 1) errors.Add(new(row, $"Có {matches.Count} bài cùng tên \"{title}\". Vui lòng tải mẫu mới để phân biệt đúng bài học."));
            if (!levels.TryGetValue(Fold(Value(levelColumn)), out var level)) errors.Add(new(row, "Mức nhận thức phải là Nhận biết, Thông hiểu hoặc Vận dụng."));
            var type = Fold(Value(typeColumn));
            if (type.Length > 0 && type != "trac nghiem" && type != Fold(MatrixQuestionTypes.MultipleChoice))
                errors.Add(new(row, "Ma trận chỉ hỗ trợ loại câu hỏi Trắc nghiệm."));
            if (!uint.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count == 0)
                errors.Add(new(row, "Số câu phải là số nguyên dương, tối đa 4294967295."));
            var normalizedPercent = percentText.TrimEnd('%').Trim().Replace(',', '.');
            if (!decimal.TryParse(normalizedPercent, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var percentage) ||
                percentage <= 0 || percentage > 100 || decimal.Round(percentage, 2) != percentage)
                errors.Add(new(row, "Tỷ lệ % phải lớn hơn 0, tối đa 100 và tối đa 2 chữ số thập phân."));
            if (matches?.Count == 1 && level is not null)
            {
                var key = (matches[0].Id, level);
                if (origins.TryGetValue(key, out var origin)) errors.Add(new(row, $"Bài học và mức nhận thức đã có ở dòng {origin}."));
                else origins.Add(key, row);
                if (errors.Count == before) details.Add(new(matches[0].Id, level, count, percentage));
            }
        }
        if (filled == 0) errors.Add(new(null, "Tệp chưa có dòng nào điền Số câu hoặc Tỷ lệ %. Điền vào tệp mẫu rồi chọn lại."));
        if (details.Aggregate(0UL, (sum, d) => sum + d.QuestionCount) > uint.MaxValue)
            errors.Add(new(null, "Tổng số câu vượt giới hạn 4294967295."));
        return Result();

        MatrixImportPreview Result() => new(errors.Count == 0, name, score,
            details.Select(d => d.LessonId).Distinct().Count(), filled, errors.Count == 0 ? details : [], errors);
    }
}
