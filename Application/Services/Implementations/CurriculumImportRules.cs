using System.Text;
using Application.DTOs;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed record PlannedLesson(string Title);

// ExistingChapterId is set when the file adds lessons to a chapter that is already stored.
public sealed record PlannedChapter(
    ulong? ExistingChapterId,
    ulong GradeLevelId,
    ulong SubjectId,
    string Title,
    IReadOnlyList<PlannedLesson> Lessons);

public sealed record CurriculumImportPlan(
    CurriculumImportPreviewDto Preview,
    IReadOnlyList<PlannedChapter> Chapters);

// Checks a whole import file against itself and against the branch's stored chapters. Pure: no I/O,
// so preview and import share it and it can be tested without a database.
// A chapter is identified by grade, subject (the "Lĩnh vực" column) and title, a lesson by its title within
// the chapter; the code columns are no longer stored and are ignored.
public static class CurriculumImportRules
{
    public const int MaxTitleLength = 255;

    // NFC, trimmed, inner whitespace collapsed: "Bài  1" typed twice must not count as two values.
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Case-insensitive, accent-sensitive; the column (utf8mb4_0900_ai_ci) is also accent-insensitive.
    public static string Fold(string? value) => Clean(value).ToUpperInvariant();

    public static CurriculumImportPlan Evaluate(
        IReadOnlyList<CurriculumImportRawRow> rows,
        IReadOnlyList<CurriculumGradeRow> grades,
        IReadOnlyList<CurriculumFieldRow> fields,
        IReadOnlyList<CurriculumChapterRow> existingChapters)
    {
        var errors = new List<CurriculumImportErrorDto>();
        var chapters = new Dictionary<(ulong Grade, ulong Subject, string Title), ChapterDraft>();

        foreach (var row in rows)
        {
            var rowErrors = new List<string>();
            var gradeName = Clean(row.Grade);
            var subjectName = Clean(row.Field);
            var chapterTitle = Clean(row.ChapterTitle);
            var lessonTitle = Clean(row.LessonTitle);

            Require(gradeName, "Khối lớp", rowErrors);
            Require(subjectName, "Lĩnh vực", rowErrors);
            Require(chapterTitle, "Tên chương", rowErrors);
            MaxLength(chapterTitle, MaxTitleLength, "Tên chương", rowErrors);
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

            CurriculumFieldRow? subject = null;
            if (subjectName.Length > 0)
            {
                var matches = fields.Where(item => Fold(item.Name) == Fold(subjectName)).ToArray();
                if (matches.Length == 0)
                {
                    rowErrors.Add($"Lĩnh vực \"{subjectName}\" không có trong danh sách hợp lệ.");
                }
                else if (matches.Length > 1)
                {
                    rowErrors.Add($"Lĩnh vực \"{subjectName}\" trùng tên nhiều môn học, không xác định được môn.");
                }
                else
                {
                    subject = matches[0];
                }
            }

            if (rowErrors.Count > 0 || grade is null || subject is null)
            {
                AddAll(errors, row.RowNumber, rowErrors);
                continue;
            }

            var key = (grade.Id, subject.Id, Fold(chapterTitle));
            if (!chapters.TryGetValue(key, out var draft))
            {
                var stored = existingChapters.FirstOrDefault(item =>
                    item.GradeLevelId == grade.Id &&
                    item.FieldId == subject.Id &&
                    Fold(item.Title) == Fold(chapterTitle));
                draft = new ChapterDraft(stored, grade.Id, subject.Id, chapterTitle, row.RowNumber);
                chapters[key] = draft;
            }

            if (lessonTitle.Length == 0)
            {
                continue;
            }

            var lessonError = draft.TryAddLesson(lessonTitle, row.RowNumber);
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
                draft.SubjectId,
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
        private readonly Dictionary<string, int> titles = new();
        private readonly List<PlannedLesson> newLessons = [];

        public ChapterDraft(
            CurriculumChapterRow? stored,
            ulong gradeLevelId,
            ulong subjectId,
            string title,
            int firstRow)
        {
            Stored = stored;
            GradeLevelId = gradeLevelId;
            SubjectId = subjectId;
            Title = title;
            FirstRow = firstRow;
            foreach (var lesson in stored?.Lessons ?? [])
            {
                titles[Fold(lesson.Title)] = 0;
            }
        }

        public CurriculumChapterRow? Stored { get; }
        public ulong GradeLevelId { get; }
        public ulong SubjectId { get; }
        public string Title { get; }
        public int FirstRow { get; }
        public IReadOnlyList<PlannedLesson> NewLessons => newLessons;

        public string? TryAddLesson(string title, int rowNumber)
        {
            if (titles.TryGetValue(Fold(title), out var titleRow))
            {
                return titleRow == 0
                    ? $"Tên bài \"{title}\" đã tồn tại trong chương \"{Title}\"."
                    : $"Tên bài \"{title}\" trùng với dòng {titleRow} trong cùng chương.";
            }

            titles[Fold(title)] = rowNumber;
            newLessons.Add(new PlannedLesson(title));
            return null;
        }
    }
}
