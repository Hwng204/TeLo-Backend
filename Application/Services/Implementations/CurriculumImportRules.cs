using System.Text;
using Application.DTOs;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed record PlannedLesson(uint Number, string Title);

// ExistingChapterId is set when the file adds lessons to a chapter that is already stored.
public sealed record PlannedChapter(
    ulong? ExistingChapterId,
    ulong GradeLevelId,
    ulong SubjectId,
    uint Number,
    string Title,
    IReadOnlyList<PlannedLesson> Lessons);

public sealed record CurriculumImportPlan(
    CurriculumImportPreviewDto Preview,
    IReadOnlyList<PlannedChapter> Chapters);

// Checks a whole import file against itself and against the branch's stored ACTIVE chapters (the only ones the
// list returns, so archived rows never match nor conflict). Pure: no I/O, so preview and import share it and it can
// be tested without a database.
// A chapter is identified by grade, subject (both chosen once at the top of the sheet) and number; its title must
// agree. Within a chapter both the lesson number and the lesson title are unique.
public static class CurriculumImportRules
{
    public const int MaxTitleLength = 255;
    public const int MaxNumber = 999;

    // NFC, trimmed, inner whitespace collapsed: "Bài  1" typed twice must not count as two values.
    public static string Clean(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Case-insensitive, accent-sensitive; the column (utf8mb4_0900_ai_ci) is also accent-insensitive.
    public static string Fold(string? value) => Clean(value).ToUpperInvariant();

    public static CurriculumImportPlan Evaluate(
        CurriculumImportSheet sheet,
        IReadOnlyList<CurriculumGradeRow> grades,
        IReadOnlyList<CurriculumFieldRow> fields,
        IReadOnlyList<CurriculumChapterRow> existingChapters)
    {
        var errors = new List<CurriculumImportErrorDto>();
        var chapters = new Dictionary<(ulong Grade, ulong Subject, uint Number), ChapterDraft>();

        // Grade and subject apply to every row, so they are reported once on their own cell; rows are not checked
        // until both are valid.
        var gradeName = Clean(sheet.Grade);
        var grade = grades.FirstOrDefault(item => Fold(item.Name) == Fold(gradeName));
        if (gradeName.Length == 0)
        {
            errors.Add(new CurriculumImportErrorDto(CurriculumWorkbook.GradeRow, "Chưa chọn Khối lớp."));
        }
        else if (grade is null)
        {
            errors.Add(new CurriculumImportErrorDto(
                CurriculumWorkbook.GradeRow, $"Khối lớp \"{gradeName}\" không có trong danh sách hợp lệ."));
        }

        var subjectName = Clean(sheet.Field);
        var subjects = fields.Where(item => Fold(item.Name) == Fold(subjectName)).ToArray();
        if (subjectName.Length == 0)
        {
            errors.Add(new CurriculumImportErrorDto(CurriculumWorkbook.FieldRow, "Chưa chọn Môn học."));
        }
        else if (subjects.Length == 0)
        {
            errors.Add(new CurriculumImportErrorDto(
                CurriculumWorkbook.FieldRow, $"Môn học \"{subjectName}\" không có trong danh sách hợp lệ."));
        }
        else if (subjects.Length > 1)
        {
            errors.Add(new CurriculumImportErrorDto(
                CurriculumWorkbook.FieldRow, $"Môn học \"{subjectName}\" trùng tên nhiều môn, không xác định được môn."));
        }

        if (errors.Count > 0)
        {
            return new CurriculumImportPlan(new CurriculumImportPreviewDto(sheet.Rows.Count, 0, 0, 0, errors), []);
        }

        var subject = subjects[0];
        foreach (var row in sheet.Rows)
        {
            var rowErrors = new List<string>();
            var chapterTitle = Clean(row.ChapterTitle);
            var lessonNumberText = Clean(row.LessonNumber);
            var lessonTitle = Clean(row.LessonTitle);

            var chapterNumber = ParseNumber(Clean(row.ChapterNumber), "Số chương", rowErrors);
            Require(chapterTitle, "Tên chương", rowErrors);
            MaxLength(chapterTitle, MaxTitleLength, "Tên chương", rowErrors);

            // A lesson needs both cells; both empty is a chapter row without a lesson.
            uint? lessonNumber = null;
            if (lessonNumberText.Length > 0 || lessonTitle.Length > 0)
            {
                lessonNumber = ParseNumber(lessonNumberText, "Số bài", rowErrors);
                Require(lessonTitle, "Tên bài", rowErrors);
                MaxLength(lessonTitle, MaxTitleLength, "Tên bài", rowErrors);
            }

            if (rowErrors.Count > 0 || chapterNumber is not { } number)
            {
                AddAll(errors, row.RowNumber, rowErrors);
                continue;
            }

            var draft = FindChapter(chapters, existingChapters, grade!.Id, subject.Id, number, chapterTitle, row.RowNumber,
                out var chapterError);
            if (draft is null)
            {
                errors.Add(new CurriculumImportErrorDto(row.RowNumber, chapterError!));
                continue;
            }

            if (lessonNumber is { } lessonNo)
            {
                AddAll(errors, row.RowNumber, draft.TryAddLesson(lessonNo, lessonTitle, row.RowNumber));
            }
        }

        var planned = chapters.Values
            .OrderBy(draft => draft.FirstRow)
            .Select(draft => new PlannedChapter(
                draft.Stored?.Id,
                draft.GradeLevelId,
                draft.SubjectId,
                draft.Number,
                draft.Title,
                draft.NewLessons))
            .ToArray();

        var preview = new CurriculumImportPreviewDto(
            sheet.Rows.Count,
            planned.Length,
            planned.Count(chapter => chapter.ExistingChapterId is null),
            planned.Sum(chapter => chapter.Lessons.Count),
            errors);
        return new CurriculumImportPlan(preview, planned);
    }

    // The chapter for (grade, subject, number), created on its first row. Null with an error when the title disagrees
    // with an earlier row or a stored chapter, or the title already belongs to another number.
    private static ChapterDraft? FindChapter(
        Dictionary<(ulong Grade, ulong Subject, uint Number), ChapterDraft> chapters,
        IReadOnlyList<CurriculumChapterRow> existingChapters,
        ulong gradeId,
        ulong subjectId,
        uint number,
        string title,
        int rowNumber,
        out string? error)
    {
        error = null;
        if (chapters.TryGetValue((gradeId, subjectId, number), out var draft))
        {
            if (Fold(draft.Title) != Fold(title))
            {
                error = $"Chương {number} đã có tên \"{draft.Title}\" ở dòng {draft.FirstRow}.";
                return null;
            }

            return draft;
        }

        var sameTitle = chapters.Values.FirstOrDefault(item =>
            item.GradeLevelId == gradeId && item.SubjectId == subjectId && Fold(item.Title) == Fold(title));
        if (sameTitle is not null)
        {
            error = $"Tên chương \"{title}\" đã dùng cho chương {sameTitle.Number} ở dòng {sameTitle.FirstRow}.";
            return null;
        }

        var scope = existingChapters.Where(item => item.GradeLevelId == gradeId && item.FieldId == subjectId).ToArray();
        var stored = scope.FirstOrDefault(item => item.Number == number);
        if (stored is not null && Fold(stored.Title) != Fold(title))
        {
            error = $"Chương {number} đã tồn tại với tên \"{stored.Title}\".";
            return null;
        }

        if (stored is null && scope.FirstOrDefault(item => Fold(item.Title) == Fold(title)) is { } other)
        {
            error = $"Tên chương \"{title}\" đã tồn tại ở chương {other.Number}.";
            return null;
        }

        draft = new ChapterDraft(stored, gradeId, subjectId, number, title, rowNumber);
        chapters[(gradeId, subjectId, number)] = draft;
        return draft;
    }

    // Digits only (no sign, no decimals), 1..MaxNumber; "01" is 1. Null with an error otherwise.
    private static uint? ParseNumber(string value, string label, List<string> errors)
    {
        if (value.Length == 0)
        {
            errors.Add($"Thiếu {label}.");
            return null;
        }

        if (value.All(char.IsAsciiDigit) && uint.TryParse(value, out var number) && number is >= 1 and <= MaxNumber)
        {
            return number;
        }

        errors.Add($"{label} phải là số nguyên từ 1 đến {MaxNumber}.");
        return null;
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
        private readonly Dictionary<uint, int> numbers = new();
        private readonly Dictionary<string, int> titles = new();
        private readonly List<PlannedLesson> newLessons = [];

        public ChapterDraft(
            CurriculumChapterRow? stored,
            ulong gradeLevelId,
            ulong subjectId,
            uint number,
            string title,
            int firstRow)
        {
            Stored = stored;
            GradeLevelId = gradeLevelId;
            SubjectId = subjectId;
            Number = number;
            Title = title;
            FirstRow = firstRow;
            foreach (var lesson in stored?.Lessons ?? [])
            {
                numbers[lesson.Number] = 0;
                titles[Fold(lesson.Title)] = 0;
            }
        }

        public CurriculumChapterRow? Stored { get; }
        public ulong GradeLevelId { get; }
        public ulong SubjectId { get; }
        public uint Number { get; }
        public string Title { get; }
        public int FirstRow { get; }
        public IReadOnlyList<PlannedLesson> NewLessons => newLessons;

        // Empty when the lesson was added; otherwise one message per clashing value.
        public List<string> TryAddLesson(uint number, string title, int rowNumber)
        {
            var clashes = new List<string>();
            if (numbers.TryGetValue(number, out var numberRow))
            {
                clashes.Add(numberRow == 0
                    ? $"Số bài {number} đã tồn tại trong chương \"{Title}\"."
                    : $"Số bài {number} trùng với dòng {numberRow} trong cùng chương.");
            }

            if (titles.TryGetValue(Fold(title), out var titleRow))
            {
                clashes.Add(titleRow == 0
                    ? $"Tên bài \"{title}\" đã tồn tại trong chương \"{Title}\"."
                    : $"Tên bài \"{title}\" trùng với dòng {titleRow} trong cùng chương.");
            }

            if (clashes.Count == 0)
            {
                numbers[number] = rowNumber;
                titles[Fold(title)] = rowNumber;
                newLessons.Add(new PlannedLesson(number, title));
            }

            return clashes;
        }
    }
}
