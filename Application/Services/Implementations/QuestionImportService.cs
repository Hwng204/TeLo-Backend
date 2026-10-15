using Application.Common;
using Application.DTOs;
using Infrastructure.Exports;

namespace Application.Services.Implement;

public sealed record QuestionImportError(int Row, string Column, string Message);
public sealed record QuestionImportResult(IReadOnlyList<QuestionImportError> Errors, IReadOnlyList<SaveQuestionRequest> Questions, QuestionPackage? Package = null);

public sealed class QuestionImportService(QuestionService service)
{
    private static string Level(string code) => MatrixLevels.GetValueOrDefault(code, code);
    private static readonly Dictionary<string, string> MatrixLevels = new() { ["NHAN_BIET"] = "Nhận biết", ["THONG_HIEU"] = "Thông hiểu", ["VAN_DUNG"] = "Vận dụng" };
    private static string Lesson(QuestionTarget target) => !string.IsNullOrEmpty(target.ChapterCode) && !string.IsNullOrEmpty(target.LessonCode)
        ? $"{target.GradeName} · {target.ChapterCode}/{target.LessonCode} · {target.LessonTitle}"
        : $"{target.GradeName} · {target.ChapterTitle} · {target.LessonTitle}";
    private async Task<QuestionPackage> PackageAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var package = await service.GetAsync(actor, id, ct);
        if (!package.CanEdit) throw new QuestionException("FORBIDDEN", "Chỉ giáo viên đang biên soạn được nhập Excel.", 403);
        return package;
    }
    public async Task<byte[]> TemplateAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        var package = await PackageAsync(actor, id, ct);
        var remaining = package.Targets.Where(t => t.Written < t.RequiredCount).Select(t => new QuestionWorkbookTarget(t.DetailId, Lesson(t), Level(t.CognitiveLevel), t.RequiredCount - (uint)t.Written)).ToArray();
        if (remaining.Length == 0) throw new QuestionException("QUESTION_QUOTA", "Gói đã đủ số câu được giao. Chỉnh sửa các câu hiện có trước khi gửi.", 409);
        try { return QuestionImportWorkbook.Template(remaining); }
        catch (InvalidDataException ex) { throw new QuestionException("IMPORT_INVALID", ex.Message); }
    }

    public async Task<byte[]> GuideAsync(QuestionActor actor, ulong id, CancellationToken ct)
    {
        await PackageAsync(actor, id, ct);
        return QuestionImportWorkbook.Guide();
    }

    public async Task<QuestionImportResult> ReadAsync(QuestionActor actor, ulong id, int version, string name, byte[] bytes, bool confirm, CancellationToken ct)
    {
        var package = await PackageAsync(actor, id, ct);
        if (package.Version != version) throw new QuestionException("QUESTION_CONFLICT", "Gói đã thay đổi. Tải lại trước khi nhập Excel.", 409);
        QuestionWorkbookRow[] rows;
        try { rows = QuestionImportWorkbook.Read(name, bytes); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FormatException or IOException)
        { throw new QuestionException("IMPORT_INVALID", "Tệp Excel không hợp lệ. " + (ex is InvalidDataException ? ex.Message : "Vui lòng sử dụng tệp mẫu.")); }
        var errors = new List<QuestionImportError>(); var questions = new List<SaveQuestionRequest>(); var seen = new HashSet<string>();
        if (rows.Length == 0) errors.Add(new(2, "Nội dung", "Tệp chưa có câu hỏi."));
        foreach (var row in rows)
        {
            var matches = row.TargetId != 0 ? package.Targets.Where(t => t.DetailId == row.TargetId).ToArray() :
                package.Targets.Where(t => Lesson(t) == row.Lesson && Level(t.CognitiveLevel) == row.Level).ToArray();
            var target = matches.Length == 1 ? matches[0] : null;
            if (target is null || !row.PrefilledContext && (Lesson(target) != row.Lesson || Level(target.CognitiveLevel) != row.Level))
            { errors.Add(new(row.Row, "Bài học/Mức nhận thức", "Không khớp nhiệm vụ. Giữ bài học và mức theo tệp mẫu.")); continue; }
            var question = new SaveQuestionRequest(version, target.DetailId, row.Content, row.Options, row.Answer, row.Explanation);
            foreach (var e in QuestionRules.DraftErrors(question)) errors.Add(new(row.Row, e.Key switch
            {
                "content" => "Nội dung câu hỏi", "options" => "A–D", "correctOption" => "Đáp án đúng", "explanation" => "Giải thích", _ => "Bài học"
            }, string.Join(" ", e.Value)));
            if (string.IsNullOrWhiteSpace(row.Content)) errors.Add(new(row.Row, "Nội dung câu hỏi", "Nhập nội dung câu hỏi."));
            var used = row.Options.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (used.Length < 2) errors.Add(new(row.Row, "A–D", "Nhập ít nhất 2 lựa chọn. Các ô không dùng để trống."));
            if (used.Distinct(StringComparer.Ordinal).Count() != used.Length) errors.Add(new(row.Row, "A–D", "Các lựa chọn đã điền phải khác nhau."));
            var answerIndex = Array.IndexOf(QuestionRules.Keys, row.Answer);
            if (answerIndex < 0) errors.Add(new(row.Row, "Đáp án đúng", "Chọn A, B, C hoặc D."));
            else if (string.IsNullOrWhiteSpace(row.Options[answerIndex])) errors.Add(new(row.Row, "Đáp án đúng", $"Lựa chọn {row.Answer} đang để trống. Chọn một lựa chọn có nội dung."));
            var fingerprint = System.Text.Json.JsonSerializer.Serialize(new { target.DetailId, row.Content, row.Options, row.Answer });
            if (!seen.Add(fingerprint)) errors.Add(new(row.Row, "Nội dung", "Câu bị lặp trong tệp."));
            questions.Add(question);
        }
        foreach (var t in package.Targets)
            if (t.Written + questions.Count(q => q.DetailId == t.DetailId) > t.RequiredCount)
                errors.Add(new(0, "Số câu", $"{t.LessonTitle} · {Level(t.CognitiveLevel)} vượt {t.RequiredCount} câu được giao."));
        if (errors.Count > 0 || !confirm) return new(errors, questions);
        return new(errors, questions, await service.ImportAsync(actor, id, version, questions, ct));
    }
}
