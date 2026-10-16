using System.Text;
using Application.DTOs;
using Application.Services.Implement;
using ClosedXML.Excel;
using Domain.Entities.Academic;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;
using Xunit;

namespace Application.Tests;

// The "Môn học" choice (B5) and the API "fields" are subjects; chapters are keyed by (grade, subject, number) and
// must agree on the title, lessons are unique by number and by title within the chapter.
public sealed class CurriculumImportFieldTests
{
    private static readonly CurriculumGradeRow[] Grades = [new(5, "Lớp 5")];
    private static readonly CurriculumFieldRow[] Fields =
    [
        new(1, "Tiếng Việt"),
        new(2, "Tiếng Anh"),
        new(3, "Toán")
    ];

    [Theory]
    [InlineData("Tiếng Việt", 1)]
    [InlineData("  tiếng   ANH ", 2)]
    [InlineData("TOÁN", 3)]
    public void SubjectName_SelectsTheSubjectWithCaseSpaceAndNfcNormalization(string subject, int expectedId)
    {
        var result = CurriculumImportRules.Evaluate(
            new CurriculumImportSheet("Lớp 5", subject.Normalize(NormalizationForm.FormD), [Row(8)]), Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal((ulong)expectedId, Assert.Single(result.Chapters).SubjectId);
    }

    [Theory]
    [InlineData("Số và phép tính")]
    [InlineData("Toan")]
    public void UnknownSubject_IsReportedOnceOnRow5(string subject)
    {
        var result = ReadAndEvaluate(Workbook("Lớp 5", subject, [Row(8), Row(9, lesson: "Bài hai", lessonNumber: "2")]));

        var error = Assert.Single(result.Preview.Errors);
        Assert.Equal((5, $"Môn học \"{subject}\" không có trong danh sách hợp lệ."), (error.RowNumber, error.Message));
        Assert.Empty(result.Chapters);
    }

    [Fact]
    public void SubjectNameSharedByTwoSubjects_IsRejectedAsAmbiguous()
    {
        CurriculumFieldRow[] fields = [.. Fields, new(4, "toán")];

        var result = CurriculumImportRules.Evaluate(Sheet(Row(8), Row(9, lesson: "Bài hai", lessonNumber: "2")),
            Grades, fields, []);

        var error = Assert.Single(result.Preview.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Contains("không xác định được môn", error.Message);
    }

    [Fact]
    public void GradeNotChosen_IsReportedOnceOnRow4_AndRowsAreNotChecked()
    {
        var result = ReadAndEvaluate(Workbook("", "Toán",
            [Row(8), Row(9, lesson: "Bài hai", lessonNumber: "2"), Row(10, chapterNumber: "x")]));

        var error = Assert.Single(result.Preview.Errors);
        Assert.Equal((4, "Chưa chọn Khối lớp."), (error.RowNumber, error.Message));
        Assert.Equal(3, result.Preview.RowCount);
        Assert.Empty(result.Chapters);
    }

    [Fact]
    public void UnknownGradeAndMissingSubject_AreEachReportedOnce()
    {
        var result = CurriculumImportRules.Evaluate(
            new CurriculumImportSheet("Lớp 9", " ", [Row(8), Row(9, chapter: "  ")]), Grades, Fields, []);

        Assert.Equal(
            new[] { (4, "Khối lớp \"Lớp 9\" không có trong danh sách hợp lệ."), (5, "Chưa chọn Môn học.") },
            result.Preview.Errors.Select(error => (error.RowNumber, error.Message)));
        Assert.Empty(result.Chapters);
    }

    [Fact]
    public void MissingChapterTitle_IsRejected()
    {
        var result = CurriculumImportRules.Evaluate(Sheet(Row(8), Row(9, chapter: "  ")), Grades, Fields, []);

        var error = Assert.Single(result.Preview.Errors);
        Assert.Equal((9, "Thiếu Tên chương."), (error.RowNumber, error.Message));
    }

    [Fact]
    public void ChapterIsKeyedByNumber_AndRowsKeepTheFileNumbers()
    {
        var result = CurriculumImportRules.Evaluate(Sheet(
            Row(8, chapter: "Số thập phân", lesson: "Khái niệm", chapterNumber: "01", lessonNumber: "1"),
            Row(9, chapter: "số  thập phân", lesson: "So sánh", chapterNumber: "1", lessonNumber: "2"),
            Row(10, chapter: "Số thập phân", lesson: "Làm tròn", chapterNumber: " 1 ", lessonNumber: "3"),
            Row(11, chapter: "Hình học", chapterNumber: "2", lesson: "", lessonNumber: "")), Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal(2, result.Preview.NewChapterCount);
        Assert.Collection(result.Chapters,
            chapter =>
            {
                Assert.Equal((5UL, 3UL, 1U, "Số thập phân"),
                    (chapter.GradeLevelId, chapter.SubjectId, chapter.Number, chapter.Title));
                Assert.Equal(new[] { (1U, "Khái niệm"), (2U, "So sánh"), (3U, "Làm tròn") },
                    chapter.Lessons.Select(lesson => (lesson.Number, lesson.Title)));
            },
            chapter => Assert.Equal((2U, "Hình học", 0), (chapter.Number, chapter.Title, chapter.Lessons.Count)));
    }

    [Theory]
    [InlineData("", "Thiếu Số chương.")]
    [InlineData("0", "Số chương phải là số nguyên từ 1 đến 999.")]
    [InlineData("1000", "Số chương phải là số nguyên từ 1 đến 999.")]
    [InlineData("-1", "Số chương phải là số nguyên từ 1 đến 999.")]
    [InlineData("1.5", "Số chương phải là số nguyên từ 1 đến 999.")]
    [InlineData("I", "Số chương phải là số nguyên từ 1 đến 999.")]
    [InlineData("99999999999999999999", "Số chương phải là số nguyên từ 1 đến 999.")]
    public void ChapterNumber_MustBeDigitsFrom1To999(string number, string message)
    {
        var result = CurriculumImportRules.Evaluate(Sheet(Row(2, chapterNumber: number)), Grades, Fields, []);

        Assert.Equal(message, Assert.Single(result.Preview.Errors).Message);
        Assert.Empty(result.Chapters);
    }

    [Theory]
    [InlineData("", "Bài thử", "Thiếu Số bài.")]
    [InlineData("2", "", "Thiếu Tên bài.")]
    [InlineData("0", "Bài thử", "Số bài phải là số nguyên từ 1 đến 999.")]
    [InlineData("1,5", "Bài thử", "Số bài phải là số nguyên từ 1 đến 999.")]
    public void LessonNumberAndTitle_AreFilledTogether_AndTheNumberIsChecked(string number, string title, string message)
    {
        var result = CurriculumImportRules.Evaluate(
            Sheet(Row(2, lesson: title, lessonNumber: number)), Grades, Fields, []);

        Assert.Equal(message, Assert.Single(result.Preview.Errors).Message);
    }

    [Fact]
    public void ChapterNumberAndTitle_MustAgreeAcrossTheRowsOfTheFile()
    {
        var result = CurriculumImportRules.Evaluate(Sheet(
        [
            Row(2, chapter: "Số thập phân", chapterNumber: "1", lesson: "Bài 1", lessonNumber: "1"),
            Row(3, chapter: "Hình học", chapterNumber: "1", lesson: "Bài 2", lessonNumber: "2"),
            Row(4, chapter: "SỐ THẬP PHÂN", chapterNumber: "2", lesson: "Bài 3", lessonNumber: "1")
        ]), Grades, Fields, []);

        Assert.Collection(result.Preview.Errors,
            error => Assert.Equal((3, "Chương 1 đã có tên \"Số thập phân\" ở dòng 2."), (error.RowNumber, error.Message)),
            error => Assert.Equal((4, "Tên chương \"SỐ THẬP PHÂN\" đã dùng cho chương 1 ở dòng 2."),
                (error.RowNumber, error.Message)));
        Assert.Equal(1, Assert.Single(result.Chapters).Lessons.Count);
    }

    [Fact]
    public void StoredChapter_IsReusedOnlyWithTheSameNumberAndTitle()
    {
        CurriculumChapterRow[] stored =
        [
            new(50, 5, "Lớp 5", 3, "Toán", 1, "Số thập phân", false, []),
            new(51, 5, "Lớp 5", 3, "Toán", 2, "Hình học", false, [])
        ];

        var result = CurriculumImportRules.Evaluate(Sheet(
        [
            Row(2, chapter: "số thập phân", chapterNumber: "1"),
            Row(3, chapter: "Đo lường", chapterNumber: "2"),
            Row(4, chapter: "Hình học", chapterNumber: "3"),
            Row(5, chapter: "Đo lường", chapterNumber: "4")
        ]), Grades, Fields, stored);

        Assert.Collection(result.Preview.Errors,
            error => Assert.Equal((3, "Chương 2 đã tồn tại với tên \"Hình học\"."), (error.RowNumber, error.Message)),
            error => Assert.Equal((4, "Tên chương \"Hình học\" đã tồn tại ở chương 2."), (error.RowNumber, error.Message)));
        Assert.Collection(result.Chapters,
            chapter => Assert.Equal((50UL, 1U), (chapter.ExistingChapterId!.Value, chapter.Number)),
            chapter => Assert.Equal((null, 4U), (chapter.ExistingChapterId, chapter.Number)));
    }

    [Fact]
    public void LessonNumbers_AreUniqueInTheChapter_AcrossRowsAndAgainstStoredLessons()
    {
        CurriculumChapterRow[] stored =
            [new(50, 5, "Lớp 5", 3, "Toán", 1, "Số thập phân", false, [new(500, 50, 1, "Khái niệm", false)])];

        var result = CurriculumImportRules.Evaluate(Sheet(
        [
            Row(2, chapter: "Số thập phân", lesson: "So sánh", lessonNumber: "1"),
            Row(3, chapter: "Số thập phân", lesson: "So sánh", lessonNumber: "2"),
            Row(4, chapter: "Số thập phân", lesson: "Làm tròn", lessonNumber: "2"),
            Row(5, chapter: "Số thập phân", lesson: "Cộng", lessonNumber: "3")
        ]), Grades, Fields, stored);

        Assert.Equal(
            new[]
            {
                (2, "Số bài 1 đã tồn tại trong chương \"Số thập phân\"."),
                (4, "Số bài 2 trùng với dòng 3 trong cùng chương.")
            },
            result.Preview.Errors.Select(error => (error.RowNumber, error.Message)));
        Assert.Equal(new[] { 2U, 3U }, Assert.Single(result.Chapters).Lessons.Select(lesson => lesson.Number));
    }

    [Fact]
    public void DuplicateLessonTitleInTheSameChapter_IsRejected_ButAllowedInAnotherChapter()
    {
        var result = CurriculumImportRules.Evaluate(Sheet(
        [
            Row(2, chapter: "Chương A", lesson: "Luyện tập chung", lessonNumber: "1"),
            Row(3, chapter: "Chương A", lesson: "luyện tập  chung", lessonNumber: "2"),
            Row(4, chapter: "Chương B", chapterNumber: "2", lesson: "Luyện tập chung")
        ]), Grades, Fields, []);

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
            new(50, 5, "Lớp 5", 3, "Toán", 1, "Số thập phân", false, [new(500, 50, 1, "Khái niệm", false)]),
            new(51, 5, "Lớp 5", 1, "Tiếng Việt", 1, "Số thập phân", false, [])
        ];

        var accepted = CurriculumImportRules.Evaluate(
            Sheet(Row(2, chapter: "SỐ THẬP PHÂN", lesson: "So sánh", lessonNumber: "2")), Grades, Fields, stored);
        var rejected = CurriculumImportRules.Evaluate(
            Sheet(Row(2, chapter: "Số thập phân", lesson: "khái niệm", lessonNumber: "2")), Grades, Fields, stored);

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
        var lists = book.Worksheet(CurriculumWorkbook.ListSheet);
        var choices = lists.Column(2).CellsUsed().Select(cell => cell.GetString()).ToArray();

        Assert.Equal(new[] { "Lớp 5" }, lists.Column(1).CellsUsed().Select(cell => cell.GetString()));
        Assert.Equal(new[] { "Tiếng Việt", "Tiếng Anh", "Toán" }, choices);
        for (var index = 0; index < choices.Length; index++)
        {
            var plan = ReadAndEvaluate(Workbook("Lớp 5", choices[index], [Row(8)], template: result.Value.Content));
            Assert.True(plan.Preview.CanImport);
            Assert.Equal((ulong)index + 1, Assert.Single(plan.Chapters).SubjectId);
        }
    }

    [Fact]
    public async Task Import_StoresActiveChapters_WithActiveEmptyContentLessons()
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);
        var result = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx",
            Workbook("Lớp 5", "Tiếng Việt",
                [Row(8, chapterNumber: "3", lessonNumber: "4"), Row(9, chapter: "Chương hai", chapterNumber: "1")]),
            default);

        Assert.Null(result.Error);
        Assert.Equal(2, result.Value!.NewChapterCount);
        Assert.Equal(new ulong[] { 1, 1 }, repository.SavedChapters.Select(chapter => chapter.SubjectId));
        Assert.Equal(new[] { (3U, 4U), (1U, 1U) },
            repository.SavedChapters.Select(chapter => (chapter.Number, chapter.Lessons.Single().Number)));
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
        var content = Workbook("Lớp 5", "Toán", [Row(8), Row(9, chapter: "Chương hai", chapterNumber: "0")]);
        var preview = await service.PreviewImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);

        Assert.Null(preview.Error);
        Assert.False(preview.Value!.CanImport);
        Assert.Equal(9, Assert.Single(preview.Value.Errors).RowNumber);
        var imported = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);
        Assert.Equal(CurriculumErrorCodes.ImportHasInvalidRows, imported.Error!.Code);
        Assert.Empty(repository.SavedChapters);
    }

    [Fact]
    public async Task CreateChapter_ValidatesSubjectNumberAndTitle()
    {
        var service = new CurriculumService(new Repository());

        var result = await service.CreateChapterAsync(
            new CurriculumActor(1, true), new SaveChapterRequest(5, 99, null, " "), default);

        Assert.Equal(CurriculumErrorCodes.Validation, result.Error!.Code);
        Assert.Equal(new[] { "fieldId", "number", "title" }, result.Error.Details!.Keys.Order());
        Assert.Equal("Số chương phải là số nguyên từ 1 đến 999.", result.Error.Details["number"].Single());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task CreateLesson_RejectsANumberOutside1To999(int number)
    {
        var result = await new CurriculumService(new Repository()).CreateLessonAsync(
            new CurriculumActor(1, true), 50, new SaveLessonRequest(number, "Bài"), default);

        Assert.Equal(CurriculumErrorCodes.Validation, result.Error!.Code);
        Assert.Equal("Số bài phải là số nguyên từ 1 đến 999.", Assert.Single(result.Error.Details!).Value.Single());
    }

    [Fact]
    public async Task CreateChapterOrLesson_WithATakenTitle_IsRejectedAsDuplicate()
    {
        var repository = new Repository { TakenTitle = "Số thập phân" };
        var service = new CurriculumService(repository);
        var actor = new CurriculumActor(1, true);

        var chapter = await service.CreateChapterAsync(actor, new SaveChapterRequest(5, 3, 1, " số thập phân "), default);
        var lesson = await service.CreateLessonAsync(actor, 50, new SaveLessonRequest(1, "Số thập phân"), default);

        Assert.Equal(CurriculumErrorCodes.ChapterDuplicate, chapter.Error!.Code);
        Assert.Equal("title", Assert.Single(chapter.Error.Details!).Key);
        Assert.Equal(CurriculumErrorCodes.LessonDuplicate, lesson.Error!.Code);
        Assert.Empty(repository.SavedChapters);
    }

    [Fact]
    public async Task DuplicateNumber_IsReportedOnTheNumber_AndWithTheTitleWhenBothClash()
    {
        var actor = new CurriculumActor(1, true);
        var numberOnly = await new CurriculumService(new Repository { TakenNumber = 2 })
            .CreateChapterAsync(actor, new SaveChapterRequest(5, 3, 2, "Chương mới"), default);
        var both = await new CurriculumService(new Repository { TakenNumber = 2, TakenTitle = "Bài cũ" })
            .UpdateLessonAsync(actor, 500, new SaveLessonRequest(2, "Bài cũ"), default);

        Assert.Equal(CurriculumErrorCodes.ChapterDuplicate, numberOnly.Error!.Code);
        Assert.Equal("Số chương 2 đã tồn tại trong khối lớp và môn học đã chọn.", numberOnly.Error.Message);
        Assert.Equal("number", Assert.Single(numberOnly.Error.Details!).Key);
        Assert.Equal(CurriculumErrorCodes.LessonDuplicate, both.Error!.Code);
        Assert.Equal(new[] { "number", "title" }, both.Error.Details!.Keys.Order());
        Assert.Equal("Số bài 2 đã tồn tại trong chương này.", both.Error.Details["number"].Single());
    }

    [Fact]
    public async Task Delete_NotInUse_RemovesTheRow()
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);

        var chapter = await service.DeleteChapterAsync(new CurriculumActor(1, true), 50, default);
        var lesson = await service.DeleteLessonAsync(new CurriculumActor(1, true), 500, default);

        Assert.False(chapter.Value!.Archived);
        Assert.False(lesson.Value!.Archived);
        Assert.Equal(2, repository.Removed.Count);
        Assert.All(repository.Loaded, row => Assert.Equal("ACTIVE", Status(row)));
    }

    [Fact]
    public async Task Delete_InUse_ArchivesInsteadOfRemoving()
    {
        var repository = new Repository { InUse = true };
        var service = new CurriculumService(repository);

        var chapter = await service.DeleteChapterAsync(new CurriculumActor(1, true), 50, default);
        var lesson = await service.DeleteLessonAsync(new CurriculumActor(1, true), 500, default);

        Assert.True(chapter.Value!.Archived);
        Assert.True(lesson.Value!.Archived);
        Assert.Empty(repository.Removed);
        Assert.Equal(new[] { "INACTIVE", "INACTIVE" }, repository.Loaded.Select(Status));
    }

    [Fact]
    public async Task Delete_BlockedByAForeignKeyMeanwhile_ReloadsAndArchives()
    {
        var repository = new Repository();
        repository.SaveResults.Enqueue(CurriculumSaveStatus.InUse);
        var service = new CurriculumService(repository);

        var result = await service.DeleteChapterAsync(new CurriculumActor(1, true), 50, default);

        Assert.True(result.Value!.Archived);
        Assert.Equal(2, repository.Loaded.Count);
        Assert.Equal("INACTIVE", Status(repository.Loaded[^1]));
    }

    [Fact]
    public void Read_OldFlatTemplate_IsRejected()
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(CurriculumWorkbook.DataSheet);
        string[] headers = ["Khối lớp (*)", "Lĩnh vực (*)", "Số chương (*)", "Tên chương (*)", "Số bài", "Tên bài"];
        string[] values = ["Lớp 5", "Toán", "1", "Chương thử", "1", "Bài thử"];
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Cell(2, index + 1).Value = values[index];
        }

        var error = Assert.Throws<CurriculumImportFormatException>(
            () => CurriculumWorkbook.Read(new MemoryStream(Bytes(book))));

        Assert.Equal("Tệp theo mẫu cũ. Vui lòng tải file mẫu mới và điền lại.", error.Message);
    }

    [Theory]
    [InlineData("A4", "Khối lớp")]
    [InlineData("A5", "Lĩnh vực (*)")]
    [InlineData("C7", "Mã bài")]
    public void Read_ChangedLabel_IsRejected(string cell, string label)
    {
        var content = Workbook("Lớp 5", "Toán", [Row(8)], sheet => sheet.Cell(cell).Value = label);

        var error = Assert.Throws<CurriculumImportFormatException>(
            () => CurriculumWorkbook.Read(new MemoryStream(content)));

        Assert.Equal("Tệp không đúng mẫu: đã đổi nhãn hoặc vị trí ô. Vui lòng tải mẫu mới và điền lại.", error.Message);
    }

    [Fact]
    public void Read_MoreThanMaxRows_IsRejected_ButExactlyMaxRowsIsRead()
    {
        var rows = Enumerable.Range(8, CurriculumWorkbook.MaxRows + 1).Select(number => Row(number)).ToArray();

        var atLimit = CurriculumWorkbook.Read(new MemoryStream(Workbook("Lớp 5", "Toán", rows[..^1])));
        var error = Assert.Throws<CurriculumImportFormatException>(
            () => CurriculumWorkbook.Read(new MemoryStream(Workbook("Lớp 5", "Toán", rows))));

        Assert.Equal(CurriculumWorkbook.MaxRows, atLimit.Rows.Count);
        Assert.Equal("Tệp có nhiều hơn 1000 dòng dữ liệu. Vui lòng chia nhỏ tệp.", error.Message);
    }

    [Fact]
    public void FilledTemplate_RoundTripsToChaptersAndLessons()
    {
        var sheet = CurriculumWorkbook.Read(new MemoryStream(Workbook("Lớp 5", "Toán",
        [
            Row(8, chapter: "Số thập phân", lesson: "Khái niệm", lessonNumber: "1"),
            Row(9, chapter: "Số thập phân", lesson: "So sánh", lessonNumber: "2"),
            Row(10, chapter: "Hình học", chapterNumber: "2", lesson: "", lessonNumber: "")
        ])));
        var plan = CurriculumImportRules.Evaluate(sheet, Grades, Fields, []);

        Assert.Equal(("Lớp 5", "Toán"), (sheet.Grade, sheet.Field));
        Assert.True(plan.Preview.CanImport);
        Assert.Equal(3, plan.Preview.RowCount);
        Assert.Collection(plan.Chapters,
            chapter =>
            {
                Assert.Equal((5UL, 3UL, 1U, "Số thập phân"),
                    (chapter.GradeLevelId, chapter.SubjectId, chapter.Number, chapter.Title));
                Assert.Equal(new[] { (1U, "Khái niệm"), (2U, "So sánh") },
                    chapter.Lessons.Select(lesson => (lesson.Number, lesson.Title)));
            },
            chapter => Assert.Equal((2U, "Hình học", 0), (chapter.Number, chapter.Title, chapter.Lessons.Count)));
    }

    [Fact]
    public void LessonRowWithBothChapterCellsEmpty_BelongsToTheChapterAbove()
    {
        var sheet = CurriculumWorkbook.Read(new MemoryStream(Workbook("Lớp 5", "Toán",
        [
            Row(8, chapter: "Số thập phân", lesson: "Khái niệm", lessonNumber: "1"),
            Row(9, chapter: "", chapterNumber: "", lesson: "So sánh", lessonNumber: "2"),
            Row(11, chapter: "", chapterNumber: "", lesson: "Làm tròn", lessonNumber: "3"),
            Row(12, chapter: "Hình học", chapterNumber: "2", lesson: "", lessonNumber: ""),
            Row(13, chapter: "", chapterNumber: "", lesson: "Góc", lessonNumber: "1")
        ])));
        var plan = CurriculumImportRules.Evaluate(sheet, Grades, Fields, []);

        Assert.Equal(
            new[]
            {
                (8, "1", "Số thập phân"), (9, "1", "Số thập phân"), (11, "1", "Số thập phân"),
                (12, "2", "Hình học"), (13, "2", "Hình học")
            },
            sheet.Rows.Select(row => (row.RowNumber, row.ChapterNumber, row.ChapterTitle)));
        Assert.True(plan.Preview.CanImport);
        Assert.Equal(new[] { 3, 1 }, plan.Chapters.Select(chapter => chapter.Lessons.Count));
    }

    [Fact]
    public void MergedChapterCells_GiveTheChapterToEveryRowOfTheMerge()
    {
        var plan = ReadAndEvaluate(Workbook("Lớp 5", "Toán",
        [
            Row(8, chapter: "Số thập phân", lesson: "Khái niệm", lessonNumber: "1"),
            Row(9, chapter: "", chapterNumber: "", lesson: "So sánh", lessonNumber: "2"),
            Row(10, chapter: "", chapterNumber: "", lesson: "Làm tròn", lessonNumber: "3")
        ], sheet =>
        {
            sheet.Range("A8:A10").Merge();
            sheet.Range("B8:B10").Merge();
        }));

        Assert.True(plan.Preview.CanImport);
        var chapter = Assert.Single(plan.Chapters);
        Assert.Equal((1U, "Số thập phân"), (chapter.Number, chapter.Title));
        Assert.Equal(new[] { 1U, 2U, 3U }, chapter.Lessons.Select(lesson => lesson.Number));
    }

    [Fact]
    public void RowWithOnlyOneChapterCellEmpty_IsReportedInsteadOfFilled()
    {
        var plan = ReadAndEvaluate(Workbook("Lớp 5", "Toán",
        [
            Row(8, chapter: "Số thập phân", lesson: "Khái niệm", lessonNumber: "1"),
            Row(9, chapter: "", lesson: "So sánh", lessonNumber: "2"),
            Row(10, chapterNumber: "", chapter: "Số thập phân", lesson: "Làm tròn", lessonNumber: "3")
        ]));

        Assert.Equal(new[] { (9, "Thiếu Tên chương."), (10, "Thiếu Số chương.") },
            plan.Preview.Errors.Select(error => (error.RowNumber, error.Message)));
    }

    [Fact]
    public void FirstRowWithoutAChapter_IsReported()
    {
        var plan = ReadAndEvaluate(Workbook("Lớp 5", "Toán",
            [Row(8, chapter: "", chapterNumber: "", lesson: "Khái niệm", lessonNumber: "1")]));

        Assert.Equal(new[] { (8, "Thiếu Số chương."), (8, "Thiếu Tên chương.") },
            plan.Preview.Errors.Select(error => (error.RowNumber, error.Message)));
    }

    [Fact]
    public void Template_HasTheChoiceBlock_TheTableRules_AndAHiddenListSheet()
    {
        using var book = new XLWorkbook(new MemoryStream(Template));
        var data = book.Worksheet(CurriculumWorkbook.DataSheet);

        Assert.Equal(XLWorksheetVisibility.Hidden, book.Worksheet(CurriculumWorkbook.ListSheet).Visibility);
        Assert.Equal(new[] { "Khối lớp (*)", "Môn học (*)" }, new[] { data.Cell("A4").GetString(), data.Cell("A5").GetString() });
        Assert.Equal(CurriculumWorkbook.Headers, data.Range("A7:D7").Cells().Select(cell => cell.GetString()));
        Assert.Equal(7, data.SheetView.SplitRow);

        foreach (var (cell, source) in new[] { ("B4", "$A$1:$A$1"), ("B5", "$B$1:$B$3") })
        {
            var list = Validation(data.Cell(cell));
            Assert.Equal(XLAllowedValues.List, list.AllowedValues);
            Assert.Contains($"{CurriculumWorkbook.ListSheet}!{source}", list.Value.Replace("'", ""));
            Assert.Equal(XLErrorStyle.Stop, list.ErrorStyle);
        }

        foreach (var cell in new[] { "A8", "C8", "A1007", "C1007" })
        {
            var number = Validation(data.Cell(cell));
            Assert.Equal((XLAllowedValues.WholeNumber, "1", "999", XLErrorStyle.Stop),
                (number.AllowedValues, number.MinValue, number.MaxValue, number.ErrorStyle));
            Assert.False(IsTextFormat(data.Cell(cell)));
        }

        Assert.True(IsTextFormat(data.Cell("B8")));
        Assert.True(IsTextFormat(data.Cell("D8")));
    }

    private static readonly byte[] Template = CurriculumWorkbook.CreateTemplate(
        Grades.Select(grade => grade.Name).ToArray(), Fields.Select(field => field.Name).ToArray());

    private static CurriculumImportSheet Sheet(params CurriculumImportRawRow[] rows) => new("Lớp 5", "Toán", rows);

    private static CurriculumImportRawRow Row(
        int number,
        string chapter = "Chương thử",
        string lesson = "Bài thử",
        string chapterNumber = "1",
        string lessonNumber = "1") =>
        new(number, chapterNumber, chapter, lessonNumber, lesson);

    private static CurriculumImportPlan ReadAndEvaluate(byte[] content) =>
        CurriculumImportRules.Evaluate(CurriculumWorkbook.Read(new MemoryStream(content)), Grades, Fields, []);

    private static string Status(object row) => row is Chapter chapter ? chapter.Status : ((Lesson)row).Status;

    // The template with Khối lớp and Môn học chosen and the rows typed into the table, then any extra edit.
    private static byte[] Workbook(
        string grade,
        string subject,
        CurriculumImportRawRow[] rows,
        Action<IXLWorksheet>? edit = null,
        byte[]? template = null)
    {
        using var book = new XLWorkbook(new MemoryStream(template ?? Template));
        var sheet = book.Worksheet(CurriculumWorkbook.DataSheet);
        sheet.Cell(CurriculumWorkbook.GradeRow, 2).Value = grade;
        sheet.Cell(CurriculumWorkbook.FieldRow, 2).Value = subject;
        foreach (var row in rows) FillRow(sheet, row);
        edit?.Invoke(sheet);
        return Bytes(book);
    }

    // Digits go into the number columns as numbers, as Excel stores them in a General-format cell.
    private static void FillRow(IXLWorksheet sheet, CurriculumImportRawRow row)
    {
        string[] values = [row.ChapterNumber, row.ChapterTitle, row.LessonNumber, row.LessonTitle];
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            var cell = sheet.Cell(row.RowNumber, index + 1);
            if (value.Length == 0) continue;
            if (index % 2 == 0 && value.All(char.IsAsciiDigit) && int.TryParse(value, out var number)) cell.Value = number;
            else cell.Value = value;
        }
    }

    private static IXLDataValidation Validation(IXLCell cell) =>
        Assert.Single(cell.Worksheet.DataValidations.GetAllInRange(cell.AsRange().RangeAddress));

    // "@" can come back as the built-in text format (id 49) after a save.
    private static bool IsTextFormat(IXLCell cell) =>
        cell.Style.NumberFormat.Format == "@" || cell.Style.NumberFormat.NumberFormatId == 49;

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
        public uint? TakenNumber { get; init; }
        public bool InUse { get; init; }
        // Results SaveAsync returns first; Saved once empty.
        public Queue<CurriculumSaveStatus> SaveResults { get; } = new();
        // Every chapter or lesson handed out by GetChapterAsync/GetLessonAsync, in order.
        public List<object> Loaded { get; } = [];
        public List<object> Removed { get; } = [];
        public Task<ulong?> GetActorBranchIdAsync(ulong id, CancellationToken ct) => Task.FromResult<ulong?>(100);
        public Task<IReadOnlyList<CurriculumGradeRow>> ListGradesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumGradeRow>>(Grades);
        public Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumFieldRow>>(Fields);
        public Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(ulong branch, CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumChapterRow>>([]);
        public Task<Chapter?> GetChapterAsync(ulong branch, ulong id, CancellationToken ct) =>
            Task.FromResult<Chapter?>(Load(new Chapter { Id = id, SchoolBranchId = branch, GradeLevelId = 5, SubjectId = 3, Number = 1, Title = "Chương" }));
        public Task<Lesson?> GetLessonAsync(ulong branch, ulong id, CancellationToken ct) =>
            Task.FromResult<Lesson?>(Load(new Lesson { Id = id, ChapterId = 50, Number = 1, Title = "Bài" }));
        public Task<(bool Number, bool Title)> FindChapterConflictAsync(ulong branch, ulong grade, ulong subject, uint number, string title, ulong? except, CancellationToken ct) =>
            Task.FromResult((number == TakenNumber, IsTaken(title)));
        public Task<(bool Number, bool Title)> FindLessonConflictAsync(ulong chapter, uint number, string title, ulong? except, CancellationToken ct) =>
            Task.FromResult((number == TakenNumber, IsTaken(title)));
        public Task<bool> IsChapterInUseAsync(ulong chapter, CancellationToken ct) => Task.FromResult(InUse);
        public Task<bool> IsLessonInUseAsync(ulong lesson, CancellationToken ct) => Task.FromResult(InUse);
        public void Add(Chapter chapter) => pendingChapters.Add(chapter);
        public void Add(Lesson lesson) => throw new NotSupportedException();
        public void Remove(Chapter chapter) => Removed.Add(chapter);
        public void Remove(Lesson lesson) => Removed.Add(lesson);
        public Task<CurriculumSaveStatus> SaveAsync(CancellationToken ct)
        {
            if (SaveResults.TryDequeue(out var result))
            {
                pendingChapters.Clear();
                return Task.FromResult(result);
            }

            SavedChapters.AddRange(pendingChapters);
            pendingChapters.Clear();
            return Task.FromResult(CurriculumSaveStatus.Saved);
        }
        private T Load<T>(T row) where T : class
        {
            Loaded.Add(row);
            return row;
        }
        private bool IsTaken(string title) => string.Equals(title, TakenTitle, StringComparison.OrdinalIgnoreCase);
    }
}
