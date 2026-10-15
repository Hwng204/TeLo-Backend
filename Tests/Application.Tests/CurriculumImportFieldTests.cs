using System.Text;
using Application.DTOs;
using Application.Services.Implement;
using ClosedXML.Excel;
using Domain.Entities.Academic;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;
using Xunit;

namespace Application.Tests;

// The "Lĩnh vực" column and the API "fields" are subjects; chapters are keyed by (grade, subject, title),
// lessons by title within the chapter, and codes are ignored.
public sealed class CurriculumImportFieldTests
{
    private static readonly CurriculumGradeRow[] Grades = [new(5, "Lớp 5")];
    private static readonly CurriculumFieldRow[] Fields =
    [
        new(1, "Tiếng Việt", 1, "Tiếng Việt"),
        new(2, "Tiếng Anh", 2, "Tiếng Anh"),
        new(3, "Toán", 3, "Toán")
    ];

    [Theory]
    [InlineData("Tiếng Việt", 1)]
    [InlineData("  tiếng   ANH ", 2)]
    [InlineData("TOÁN", 3)]
    public void SubjectName_SelectsTheSubjectWithCaseSpaceAndNfcNormalization(string subject, int expectedId)
    {
        var result = CurriculumImportRules.Evaluate(
            [Row(2, subject.Normalize(NormalizationForm.FormD))], Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal((ulong)expectedId, Assert.Single(result.Chapters).SubjectId);
    }

    [Theory]
    [InlineData("Số và phép tính")]
    [InlineData("Toan")]
    public void UnknownSubject_IsRejected(string subject)
    {
        var result = CurriculumImportRules.Evaluate([Row(2, subject)], Grades, Fields, []);

        Assert.False(result.Preview.CanImport);
        Assert.Contains("không có trong danh sách", Assert.Single(result.Preview.Errors).Message);
    }

    [Fact]
    public void SubjectNameSharedByTwoSubjects_IsRejectedAsAmbiguous()
    {
        CurriculumFieldRow[] fields = [.. Fields, new(4, "toán", 4, "toán")];

        var result = CurriculumImportRules.Evaluate([Row(2, "Toán")], Grades, fields, []);

        Assert.False(result.Preview.CanImport);
        Assert.Contains("không xác định được môn", Assert.Single(result.Preview.Errors).Message);
    }

    [Fact]
    public void UnknownGradeOrMissingChapterTitle_IsRejected()
    {
        var result = CurriculumImportRules.Evaluate(
            [Row(2, "Toán", grade: "Lớp 9"), Row(3, "Toán", chapter: "  ")], Grades, Fields, []);

        Assert.False(result.Preview.CanImport);
        Assert.Equal(new[] { 2, 3 }, result.Preview.Errors.Select(error => error.RowNumber));
        Assert.Empty(result.Chapters);
    }

    [Fact]
    public void ChapterIsKeyedByGradeSubjectAndTitle_CodesAreIgnored()
    {
        var result = CurriculumImportRules.Evaluate(
        [
            Row(2, "Toán", chapter: "Số thập phân", lesson: "Khái niệm", chapterCode: "1", lessonCode: "1"),
            Row(3, "Toán", chapter: "số  thập phân", lesson: "So sánh", chapterCode: "9", lessonCode: "1"),
            Row(4, "Toán", chapter: "Số thập phân", lesson: "Làm tròn", chapterCode: "", lessonCode: ""),
            Row(5, "Tiếng Việt", chapter: "Số thập phân", lesson: "Khái niệm"),
            Row(6, "Toán", chapter: "Hình học", lesson: "", lessonCode: "7")
        ], Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal(3, result.Preview.NewChapterCount);
        Assert.Collection(result.Chapters,
            chapter =>
            {
                Assert.Equal((3UL, "Số thập phân"), (chapter.SubjectId, chapter.Title));
                Assert.Equal(new[] { "Khái niệm", "So sánh", "Làm tròn" }, chapter.Lessons.Select(lesson => lesson.Title));
            },
            chapter => Assert.Equal((1UL, 1), (chapter.SubjectId, chapter.Lessons.Count)),
            chapter => Assert.Equal(("Hình học", 0), (chapter.Title, chapter.Lessons.Count)));
    }

    [Fact]
    public void DuplicateLessonTitleInTheSameChapter_IsRejected_ButAllowedInAnotherChapter()
    {
        var result = CurriculumImportRules.Evaluate(
        [
            Row(2, "Toán", chapter: "Chương A", lesson: "Luyện tập chung", lessonCode: "1"),
            Row(3, "Toán", chapter: "Chương A", lesson: "luyện tập  chung", lessonCode: "2"),
            Row(4, "Toán", chapter: "Chương B", lesson: "Luyện tập chung")
        ], Grades, Fields, []);

        var error = Assert.Single(result.Preview.Errors);
        Assert.Equal(3, error.RowNumber);
        Assert.Contains("trùng với dòng 2", error.Message);
        Assert.Equal(new[] { 1, 1 }, result.Chapters.Select(chapter => chapter.Lessons.Count));
    }

    [Fact]
    public void StoredChapter_ReceivesNewLessons_AndRejectsAStoredLessonTitle()
    {
        CurriculumChapterRow[] stored =
        [
            new(50, 5, "Lớp 5", 3, "Toán", "", "Số thập phân", 1, [new(500, 50, "", "Khái niệm", 1, false)]),
            new(51, 5, "Lớp 5", 1, "Tiếng Việt", "", "Số thập phân", 1, [])
        ];

        var accepted = CurriculumImportRules.Evaluate(
            [Row(2, "Toán", chapter: "SỐ THẬP PHÂN", lesson: "So sánh")], Grades, Fields, stored);
        var rejected = CurriculumImportRules.Evaluate(
            [Row(2, "Toán", chapter: "Số thập phân", lesson: "khái niệm")], Grades, Fields, stored);

        Assert.True(accepted.Preview.CanImport);
        Assert.Equal(0, accepted.Preview.NewChapterCount);
        var chapter = Assert.Single(accepted.Chapters);
        Assert.Equal(50UL, chapter.ExistingChapterId);
        Assert.Equal("So sánh", Assert.Single(chapter.Lessons).Title);
        Assert.Contains("đã tồn tại trong chương", Assert.Single(rejected.Preview.Errors).Message);
    }

    [Fact]
    public async Task EveryTemplateSubjectChoice_RoundTripsToThatSubject()
    {
        var service = new CurriculumService(new Repository());
        var result = await service.BuildTemplateAsync(default);
        Assert.Null(result.Error);
        using var book = new XLWorkbook(new MemoryStream(result.Value!.Content));
        var guide = book.Worksheet(CurriculumWorkbook.GuideSheet);
        var firstChoice = guide.RowsUsed().Single(row => row.Cell(1).GetString() == "Khối lớp hợp lệ").RowNumber() + 1;
        var choices = guide.Rows(firstChoice, firstChoice + Fields.Length - 1)
            .Select(row => row.Cell(2).GetString()).ToArray();

        Assert.Equal(new[] { "Tiếng Việt", "Tiếng Anh", "Toán" }, choices);
        for (var index = 0; index < choices.Length; index++)
        {
            using var choiceBook = new XLWorkbook(new MemoryStream(result.Value.Content));
            FillRow(choiceBook.Worksheet(CurriculumWorkbook.DataSheet), Row(2, choices[index]));
            using var stream = new MemoryStream(Bytes(choiceBook));
            var plan = CurriculumImportRules.Evaluate(CurriculumWorkbook.Read(stream), Grades, Fields, []);
            Assert.True(plan.Preview.CanImport);
            Assert.Equal((ulong)index + 1, Assert.Single(plan.Chapters).SubjectId);
        }
    }

    [Fact]
    public async Task Import_StoresOneActiveChapterPerSubject_WithActiveEmptyContentLessons()
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);
        var result = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx",
            Workbook(Row(2, "Tiếng Việt"), Row(3, "Tiếng Anh", chapterCode: "")), default);

        Assert.Null(result.Error);
        Assert.Equal(2, result.Value!.NewChapterCount);
        Assert.Equal(new ulong[] { 1, 2 }, repository.SavedChapters.Select(chapter => chapter.SubjectId));
        Assert.All(repository.SavedChapters, chapter =>
        {
            Assert.Equal((100UL, 5UL, "ACTIVE"), (chapter.SchoolBranchId, chapter.GradeLevelId, chapter.Status));
            var lesson = Assert.Single(chapter.Lessons);
            Assert.Equal(("Bài thử", "", "ACTIVE"), (lesson.Title, lesson.Content, lesson.Status));
        });
    }

    [Fact]
    public async Task InvalidRow_DoesNotStoreTheOtherValidRow()
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);
        var content = Workbook(Row(2, "Toán"), Row(3, "Số và phép tính"));
        var preview = await service.PreviewImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);

        Assert.Null(preview.Error);
        Assert.False(preview.Value!.CanImport);
        Assert.Equal(3, Assert.Single(preview.Value.Errors).RowNumber);
        var imported = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);
        Assert.Equal(CurriculumErrorCodes.ImportHasInvalidRows, imported.Error!.Code);
        Assert.Empty(repository.SavedChapters);
    }

    [Fact]
    public async Task CreateChapter_ValidatesSubjectAndTitle_NotCode()
    {
        var service = new CurriculumService(new Repository());

        var result = await service.CreateChapterAsync(
            new CurriculumActor(1, true), new SaveChapterRequest(5, 99, " "), default);

        Assert.Equal(CurriculumErrorCodes.Validation, result.Error!.Code);
        Assert.Equal(new[] { "fieldId", "title" }, result.Error.Details!.Keys.Order());
    }

    [Fact]
    public async Task CreateChapterOrLesson_WithATakenTitle_IsRejectedAsDuplicate()
    {
        var repository = new Repository { TakenTitle = "Số thập phân" };
        var service = new CurriculumService(repository);
        var actor = new CurriculumActor(1, true);

        var chapter = await service.CreateChapterAsync(actor, new SaveChapterRequest(5, 3, " số thập phân "), default);
        var lesson = await service.CreateLessonAsync(actor, 50, new SaveLessonRequest("Số thập phân"), default);

        Assert.Equal(CurriculumErrorCodes.ChapterDuplicate, chapter.Error!.Code);
        Assert.Equal("title", Assert.Single(chapter.Error.Details!).Key);
        Assert.Equal(CurriculumErrorCodes.LessonDuplicate, lesson.Error!.Code);
        Assert.Empty(repository.SavedChapters);
    }

    private static CurriculumImportRawRow Row(
        int number,
        string subject,
        string chapter = "Chương thử",
        string lesson = "Bài thử",
        string chapterCode = "1",
        string lessonCode = "1",
        string grade = "Lớp 5") =>
        new(number, grade, subject, chapterCode, chapter, lessonCode, lesson);

    private static byte[] Workbook(params CurriculumImportRawRow[] rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(CurriculumWorkbook.DataSheet);
        for (var index = 0; index < CurriculumWorkbook.Headers.Length; index++)
            sheet.Cell(1, index + 1).Value = CurriculumWorkbook.Headers[index];
        foreach (var row in rows) FillRow(sheet, row);
        return Bytes(book);
    }

    private static void FillRow(IXLWorksheet sheet, CurriculumImportRawRow row)
    {
        var values = new[] { row.Grade, row.Field, row.ChapterCode, row.ChapterTitle, row.LessonCode, row.LessonTitle };
        for (var index = 0; index < values.Length; index++) sheet.Cell(row.RowNumber, index + 1).Value = values[index];
    }

    private static byte[] Bytes(XLWorkbook book)
    {
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class Repository : ICurriculumRepository
    {
        private readonly List<Chapter> pendingChapters = [];
        public List<Chapter> SavedChapters { get; } = [];
        // Compared like the column collation (case-insensitive): the service sends cleaned titles.
        public string? TakenTitle { get; init; }
        public Task<ulong?> GetActorBranchIdAsync(ulong id, CancellationToken ct) => Task.FromResult<ulong?>(100);
        public Task<IReadOnlyList<CurriculumGradeRow>> ListGradesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumGradeRow>>(Grades);
        public Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumFieldRow>>(Fields);
        public Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(ulong branch, CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumChapterRow>>([]);
        public Task<Chapter?> GetChapterAsync(ulong branch, ulong id, CancellationToken ct) =>
            Task.FromResult<Chapter?>(new Chapter { Id = id, SchoolBranchId = branch, GradeLevelId = 5, SubjectId = 3, Title = "Chương" });
        public Task<bool> IsChapterTitleTakenAsync(ulong branch, ulong grade, ulong subject, string title, ulong? except, CancellationToken ct) => Task.FromResult(IsTaken(title));
        public Task<bool> IsLessonTitleTakenAsync(ulong chapter, string title, ulong? except, CancellationToken ct) => Task.FromResult(IsTaken(title));
        public void Add(Chapter chapter) => pendingChapters.Add(chapter);
        public Task<CurriculumSaveStatus> SaveAsync(CancellationToken ct)
        {
            SavedChapters.AddRange(pendingChapters);
            pendingChapters.Clear();
            return Task.FromResult(CurriculumSaveStatus.Saved);
        }
        public Task<Lesson?> GetLessonAsync(ulong branch, ulong id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsChapterInUseAsync(ulong chapter, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsLessonInUseAsync(ulong lesson, CancellationToken ct) => throw new NotSupportedException();
        public void Add(Lesson lesson) => throw new NotSupportedException();
        public void Remove(Chapter chapter) => throw new NotSupportedException();
        public void Remove(Lesson lesson) => throw new NotSupportedException();
        private bool IsTaken(string title) => string.Equals(title, TakenTitle, StringComparison.OrdinalIgnoreCase);
    }
}
