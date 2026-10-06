using System.Text;
using Application.DTOs;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed record PlannedLesson(string Code, string Title);

// ExistingChapterId is set when the file adds lessons to a chapter that is already stored.
public sealed record PlannedChapter(
    ulong? ExistingChapterId,
    ulong GradeLevelId,
    ulong FieldId,
    string Code,
    string Title,
    IReadOnlyList<PlannedLesson> Lessons);

public sealed record CurriculumImportPlan(
    CurriculumImportPreviewDto Preview,
    IReadOnlyList<PlannedChapter> Chapters);

// Checks a whole import file against itself and against the branch's stored chapters. Pure: no I/O,
// so preview and import share it and it can be tested without a database.
public static class CurriculumImportRules
{
    public const int MaxCodeLength = 32;
    public const int MaxTitleLength = 255;

    // NFC, trimmed, inner whitespace collapsed: "Bài  1" typed twice must not count as two values.
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Same rule as the column collation (utf8mb4_0900_as_ci): case-insensitive, accent-sensitive.
    public static string Fold(string? value) => Clean(value).ToUpperInvariant();

    public static CurriculumImportPlan Evaluate(
        IReadOnlyList<CurriculumImportRawRow> rows,
        IReadOnlyList<CurriculumGradeRow> grades,
        IReadOnlyList<CurriculumFieldRow> fields,
        IReadOnlyList<CurriculumChapterRow> existingChapters)
    {
        var errors = new List<CurriculumImportErrorDto>();
        var chapters = new Dictionary<(ulong Grade, ulong Field, string Code), ChapterDraft>();
        var titleOwners = new Dictionary<(ulong Grade, ulong Field, string Title), (string Code, int Row)>();

        foreach (var row in rows)
        {
            var rowErrors = new List<string>();
            var gradeName = Clean(row.Grade);
            var fieldName = Clean(row.Field);
            var chapterCode = Clean(row.ChapterCode);
            var chapterTitle = Clean(row.ChapterTitle);
            var lessonCode = Clean(row.LessonCode);
            var lessonTitle = Clean(row.LessonTitle);

            Require(gradeName, "Khối lớp", rowErrors);
            Require(fieldName, "Lĩnh vực", rowErrors);
            Require(chapterCode, "Mã chương", rowErrors);
            Require(chapterTitle, "Tên chương", rowErrors);
            if ((lessonCode.Length == 0) != (lessonTitle.Length == 0))
            {
                rowErrors.Add("Mã bài và Tên bài phải điền cả hai hoặc bỏ trống cả hai.");
            }

            MaxLength(chapterCode, MaxCodeLength, "Mã chương", rowErrors);
            MaxLength(chapterTitle, MaxTitleLength, "Tên chương", rowErrors);
            MaxLength(lessonCode, MaxCodeLength, "Mã bài", rowErrors);
            MaxLength(lessonTitle, MaxTitleLength, "Tên bài", rowErrors);

            CurriculumGradeRow? grade = null;
            if (gradeName.Length > 0)
            {
                grade = grades.FirstOrDefault(item => Fold(item.Name) == Fold(gradeName));
                if (grade is null)
                {
                    rowErrors.Add($"Khối lớp \"{gradeName}\" không có trong danh sách hợp lệ.");
                }
            }

            CurriculumFieldRow? field = null;
            if (fieldName.Length > 0)
            {
                var matches = fields.Where(item => Fold(item.Name) == Fold(fieldName)).ToArray();
                if (matches.Length == 0)
                {
                    rowErrors.Add($"Lĩnh vực \"{fieldName}\" không có trong danh sách hợp lệ.");
                }
                else if (matches.Length > 1)
                {
                    rowErrors.Add($"Lĩnh vực \"{fieldName}\" có ở nhiều môn học, không xác định được môn.");
                }
                else
                {
                    field = matches[0];
                }
            }

            if (rowErrors.Count > 0 || grade is null || field is null)
            {
                AddAll(errors, row.RowNumber, rowErrors);
                continue;
            }

            var key = (grade.Id, field.Id, Fold(chapterCode));
            if (!chapters.TryGetValue(key, out var draft))
            {
                var titleKey = (grade.Id, field.Id, Fold(chapterTitle));
                if (titleOwners.TryGetValue(titleKey, out var owner))
                {
                    errors.Add(new CurriculumImportErrorDto(
                        row.RowNumber,
                        $"Tên chương \"{chapterTitle}\" đã dùng cho mã chương \"{owner.Code}\" ở dòng {owner.Row}."));
                    continue;
                }

                var sameGroup = existingChapters
                    .Where(item => item.GradeLevelId == grade.Id && item.FieldId == field.Id)
                    .ToArray();
                var stored = sameGroup.FirstOrDefault(item => Fold(item.Code) == Fold(chapterCode));
                if (stored is not null && Fold(stored.Title) != Fold(chapterTitle))
                {
                    errors.Add(new CurriculumImportErrorDto(
                        row.RowNumber,
                        $"Mã chương \"{chapterCode}\" đã tồn tại trong khối lớp và lĩnh vực này với tên \"{stored.Title}\"."));
                    continue;
                }

                if (stored is null && sameGroup.Any(item => Fold(item.Title) == Fold(chapterTitle)))
                {
                    errors.Add(new CurriculumImportErrorDto(
                        row.RowNumber,
                        $"Tên chương \"{chapterTitle}\" đã tồn tại trong khối lớp và lĩnh vực này với mã chương khác."));
                    continue;
                }

                draft = new ChapterDraft(stored, grade.Id, field.Id, chapterCode, chapterTitle, row.RowNumber);
                chapters[key] = draft;
                titleOwners[titleKey] = (chapterCode, row.RowNumber);
            }
            else if (Fold(draft.Title) != Fold(chapterTitle))
            {
                errors.Add(new CurriculumImportErrorDto(
                    row.RowNumber,
                    $"Tên chương khác với dòng {draft.FirstRow} có cùng mã chương \"{draft.Code}\"."));
                continue;
            }

            if (lessonCode.Length == 0)
            {
                continue;
            }

            var lessonError = draft.TryAddLesson(lessonCode, lessonTitle, row.RowNumber);
            if (lessonError is not null)
            {
                errors.Add(new CurriculumImportErrorDto(row.RowNumber, lessonError));
            }
        }

        var planned = chapters.Values
            .OrderBy(draft => draft.FirstRow)
            .Select(draft => new PlannedChapter(
                draft.Stored?.Id,
                draft.GradeLevelId,
                draft.FieldId,
                draft.Code,
                draft.Title,
                draft.NewLessons))
            .ToArray();

        var preview = new CurriculumImportPreviewDto(
            rows.Count,
            planned.Length,
            planned.Count(chapter => chapter.ExistingChapterId is null),
            planned.Sum(chapter => chapter.Lessons.Count),
            errors);
        return new CurriculumImportPlan(preview, planned);
    }

    private static void Require(string value, string label, List<string> errors)
    {
        if (value.Length == 0)
        {
            errors.Add($"Thiếu {label}.");
        }
    }

    private static void MaxLength(string value, int max, string label, List<string> errors)
    {
        if (value.Length > max)
        {
            errors.Add($"{label} vượt quá độ dài tối đa {max} ký tự.");
        }
    }

    private static void AddAll(List<CurriculumImportErrorDto> errors, int rowNumber, IEnumerable<string> messages) =>
        errors.AddRange(messages.Select(message => new CurriculumImportErrorDto(rowNumber, message)));

    private sealed class ChapterDraft
    {
        // Row 0 marks a lesson that is already stored in the chapter.
        private readonly Dictionary<string, int> codes = new();
        private readonly Dictionary<string, int> titles = new();
        private readonly List<PlannedLesson> newLessons = [];

        public ChapterDraft(
            CurriculumChapterRow? stored,
            ulong gradeLevelId,
            ulong fieldId,
            string code,
            string title,
            int firstRow)
        {
            Stored = stored;
            GradeLevelId = gradeLevelId;
            FieldId = fieldId;
            Code = code;
            Title = title;
            FirstRow = firstRow;
            foreach (var lesson in stored?.Lessons ?? [])
            {
                codes[Fold(lesson.Code)] = 0;
                titles[Fold(lesson.Title)] = 0;
            }
        }

        public CurriculumChapterRow? Stored { get; }
        public ulong GradeLevelId { get; }
        public ulong FieldId { get; }
        public string Code { get; }
        public string Title { get; }
        public int FirstRow { get; }
        public IReadOnlyList<PlannedLesson> NewLessons => newLessons;

        public string? TryAddLesson(string code, string title, int rowNumber)
        {
            if (codes.TryGetValue(Fold(code), out var codeRow))
            {
                return codeRow == 0
                    ? $"Mã bài \"{code}\" đã tồn tại trong chương \"{Title}\"."
                    : $"Mã bài \"{code}\" trùng với dòng {codeRow} trong cùng chương.";
            }

            if (titles.TryGetValue(Fold(title), out var titleRow))
            {
                return titleRow == 0
                    ? $"Tên bài \"{title}\" đã tồn tại trong chương \"{Title}\"."
                    : $"Tên bài \"{title}\" trùng với dòng {titleRow} trong cùng chương.";
            }

            codes[Fold(code)] = rowNumber;
            titles[Fold(title)] = rowNumber;
            newLessons.Add(new PlannedLesson(code, title));
            return null;
        }
    }
}
