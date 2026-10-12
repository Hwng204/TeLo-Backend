using System.Text;
using Application.DTOs;
using Application.Services.Implement;
using ClosedXML.Excel;
using Domain.Entities.Academic;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;
using Xunit;

namespace Application.Tests;

public sealed class CurriculumImportFieldTests
{
    private static readonly CurriculumGradeRow[] Grades = [new(5, "Lớp 5")];
    private static readonly CurriculumFieldRow[] Fields =
    [
        new(1, "Chưa phân loại", 1, "Tiếng Việt"),
        new(2, "chưa phân loại", 2, "Tiếng Anh"),
        new(3, "Số và phép tính", 3, "Toán")
    ];

    [Theory]
    [InlineData("Tiếng Việt — Chưa phân loại", 1)]
    [InlineData("tiếng anh — CHƯA PHÂN LOẠI", 2)]
    public void QualifiedDuplicateField_SelectsItsSubjectWithCaseAndNfcNormalization(string field, int expectedId)
    {
        var result = CurriculumImportRules.Evaluate(
            [Row(2, field.Normalize(NormalizationForm.FormD))], Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal((ulong)expectedId, Assert.Single(result.Chapters).FieldId);
    }

    [Fact]
    public void UniqueBareFieldName_RemainsAccepted()
    {
        var result = CurriculumImportRules.Evaluate([Row(2, "  SỐ  và phép tính ")], Grades, Fields, []);

        Assert.True(result.Preview.CanImport);
        Assert.Equal(3UL, Assert.Single(result.Chapters).FieldId);
    }

    [Theory]
    [InlineData("Chưa phân loại")]
    [InlineData("Toán — Chưa phân loại")]
    public void AmbiguousOrWrongSubjectField_IsRejected(string field)
    {
        var result = CurriculumImportRules.Evaluate([Row(2, field)], Grades, Fields, []);

        Assert.False(result.Preview.CanImport);
        Assert.Equal(2, Assert.Single(result.Preview.Errors).RowNumber);
    }

    [Fact]
    public async Task EveryTemplateFieldChoice_RoundTripsToTheIntendedSubject()
    {
        var service = new CurriculumService(new Repository());
        var result = await service.BuildTemplateAsync(default);
        Assert.Null(result.Error);
        using var book = new XLWorkbook(new MemoryStream(result.Value!.Content));
        var guide = book.Worksheet(CurriculumWorkbook.GuideSheet);
        var firstChoice = guide.RowsUsed().Single(row => row.Cell(1).GetString() == "Khối lớp hợp lệ").RowNumber() + 1;
        var choices = guide.Rows(firstChoice, firstChoice + Fields.Length - 1)
            .Select(row => row.Cell(2).GetString()).ToArray();

        Assert.Equal(new[] { "Tiếng Việt — Chưa phân loại", "Tiếng Anh — chưa phân loại", "Số và phép tính" }, choices);
        for (var index = 0; index < choices.Length; index++)
        {
            using var choiceBook = new XLWorkbook(new MemoryStream(result.Value.Content));
            FillRow(choiceBook.Worksheet(CurriculumWorkbook.DataSheet), 2, choices[index]);
            using var stream = new MemoryStream(Bytes(choiceBook));
            var plan = CurriculumImportRules.Evaluate(CurriculumWorkbook.Read(stream), Grades, Fields, []);
            Assert.True(plan.Preview.CanImport);
            Assert.Equal((ulong)index + 1, Assert.Single(plan.Chapters).FieldId);
        }
    }

    [Fact]
    public async Task QualifiedFields_StoreSeparateChaptersInTheSelectedSubjects()
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);
        var result = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx",
            Workbook("Tiếng Việt — Chưa phân loại", "Tiếng Anh — chưa phân loại"), default);

        Assert.Null(result.Error);
        Assert.Equal(2, result.Value!.NewChapterCount);
        Assert.Equal(new ulong[] { 1, 2 }, repository.SavedChapters.Select(chapter => chapter.FieldId));
        Assert.All(repository.SavedChapters, chapter => Assert.Single(chapter.Lessons));
    }

    [Theory]
    [InlineData("Chưa phân loại")]
    [InlineData("Toán — Chưa phân loại")]
    public async Task InvalidFieldRow_DoesNotStoreTheOtherValidRow(string invalidField)
    {
        var repository = new Repository();
        var service = new CurriculumService(repository);
        var content = Workbook("Số và phép tính", invalidField);
        var preview = await service.PreviewImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);

        Assert.Null(preview.Error);
        Assert.False(preview.Value!.CanImport);
        Assert.Equal(3, Assert.Single(preview.Value.Errors).RowNumber);
        var imported = await service.ImportAsync(new CurriculumActor(1, true), "curriculum.xlsx", content, default);
        Assert.Equal(CurriculumErrorCodes.ImportHasInvalidRows, imported.Error!.Code);
        Assert.Empty(repository.SavedChapters);
    }

    private static CurriculumImportRawRow Row(int number, string field) =>
        new(number, "Lớp 5", field, "1", "Chương thử", "1", "Bài thử");

    private static byte[] Workbook(params string[] fields)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet(CurriculumWorkbook.DataSheet);
        for (var index = 0; index < CurriculumWorkbook.Headers.Length; index++)
            sheet.Cell(1, index + 1).Value = CurriculumWorkbook.Headers[index];
        for (var index = 0; index < fields.Length; index++) FillRow(sheet, index + 2, fields[index]);
        return Bytes(book);
    }

    private static void FillRow(IXLWorksheet sheet, int number, string field)
    {
        var row = Row(number, field);
        var values = new[] { row.Grade, row.Field, row.ChapterCode, row.ChapterTitle, row.LessonCode, row.LessonTitle };
        for (var index = 0; index < values.Length; index++) sheet.Cell(number, index + 1).Value = values[index];
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
        public Task<ulong?> GetActorBranchIdAsync(ulong id, CancellationToken ct) => Task.FromResult<ulong?>(100);
        public Task<IReadOnlyList<CurriculumGradeRow>> ListGradesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumGradeRow>>(Grades);
        public Task<IReadOnlyList<CurriculumFieldRow>> ListFieldsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumFieldRow>>(Fields);
        public Task<IReadOnlyList<CurriculumChapterRow>> ListChaptersAsync(ulong branch, CancellationToken ct) => Task.FromResult<IReadOnlyList<CurriculumChapterRow>>([]);
        public Task<uint> NextChapterSortOrderAsync(ulong branch, ulong grade, CancellationToken ct) => Task.FromResult(1U);
        public void Add(Chapter chapter) => pendingChapters.Add(chapter);
        public Task<CurriculumSaveStatus> SaveAsync(CancellationToken ct)
        {
            SavedChapters.AddRange(pendingChapters);
            pendingChapters.Clear();
            return Task.FromResult(CurriculumSaveStatus.Saved);
        }
        public Task<Chapter?> GetChapterAsync(ulong branch, ulong id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Lesson?> GetLessonAsync(ulong branch, ulong id, CancellationToken ct) => throw new NotSupportedException();
        public Task<(bool, bool)> FindChapterConflictAsync(ulong branch, ulong grade, ulong field, string code, string title, ulong? except, CancellationToken ct) => throw new NotSupportedException();
        public Task<(bool, bool)> FindLessonConflictAsync(ulong chapter, string code, string title, ulong? except, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsChapterInUseAsync(ulong chapter, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsLessonInUseAsync(ulong lesson, CancellationToken ct) => throw new NotSupportedException();
        public Task<uint> NextLessonSortOrderAsync(ulong chapter, CancellationToken ct) => throw new NotSupportedException();
        public void Add(Lesson lesson) => throw new NotSupportedException();
        public void Remove(Chapter chapter) => throw new NotSupportedException();
        public void Remove(Lesson lesson) => throw new NotSupportedException();
    }
}
