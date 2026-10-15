using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Xunit;

namespace Application.Tests;

public sealed class MatrixImportServiceTests
{
    private const ulong ScopeKey = 100_040_050; // new MatrixScope(100, 40, 50).Key
    private static readonly MatrixImportContext Context = new(ScopeKey, 10, "Toán");

    [Fact]
    public async Task Template_ContainsScopedLessonsAndThreeLevels()
    {
        var repository = new References();
        var file = await Service(repository).BuildTemplateAsync(Context, default);
        var sheet = MatrixImportWorkbook.Read(file.Content);
        Assert.Equal($"mau-ma-tran-{ScopeKey}.xlsx", file.FileName);
        Assert.Equal(11, sheet.Rows.Count);
        Assert.All(sheet.Rows.Skip(8), row => Assert.Equal("Bài Phân số — Chương Số", row[0]));
        Assert.Equal(new MatrixActor(1, MatrixActorRole.Pht, 100), repository.Actor);
    }

    [Theory]
    [InlineData(0UL, 10, "InvalidRequest")]
    [InlineData(ScopeKey + 1, 10, "InvalidReference")]
    [InlineData(ScopeKey, 0, "InvalidRequest")]
    [InlineData(ScopeKey, 1000, "InvalidRequest")]
    public async Task InvalidScopeOrScore_IsRejected(ulong key, int score, string code)
    {
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            Service(new References()).BuildTemplateAsync(new(key, score), default));
        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData("file.xls", 1)]
    [InlineData("file.csv", 1)]
    [InlineData("file.xlsx", 0)]
    [InlineData("file.xlsx", MatrixImportWorkbook.MaxFileBytes + 1)]
    [InlineData("file.xlsx", 10)]
    public async Task InvalidFile_IsRejected(string name, int length)
    {
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            Service(new References()).PreviewAsync(Context, name, new byte[length], default));
        Assert.Equal("ImportFileInvalid", error.Code);
    }

    [Fact]
    public async Task Confirm_RechecksReferencesChangedAfterPreview()
    {
        var repository = new References();
        var service = Service(repository);
        var bytes = ValidFile();
        Assert.True((await service.PreviewAsync(Context, "matrix.xlsx", bytes, default)).CanImport);
        repository.Lessons = [];
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() => service.ImportAsync(Context, "matrix.xlsx", bytes, default));
        Assert.Equal("ImportHasInvalidRows", error.Code);
        Assert.Equal(2, repository.Reads);
    }

    [Fact]
    public async Task Confirm_ReturnsValidatedDraftDetailsWithoutPersisting()
    {
        var preview = await Service(new References()).ImportAsync(Context, "matrix.XLSX", ValidFile(), default);
        Assert.True(preview.CanImport);
        Assert.Equal(1UL, Assert.Single(preview.Details).LessonId);
    }

    [Fact]
    public async Task BranchRestriction_IsNotBypassed()
    {
        var repository = new References { Forbidden = true };
        var error = await Assert.ThrowsAsync<MatrixDomainException>(() => Service(repository).BuildTemplateAsync(Context, default));
        Assert.Equal("Forbidden", error.Code);
    }

    private static byte[] ValidFile()
    {
        using var book = new ClosedXML.Excel.XLWorkbook();
        var sheet = book.AddWorksheet("Matrix");
        for (var i = 0; i < MatrixImportWorkbook.Headers.Length; i++) sheet.Cell(1, i + 1).Value = MatrixImportWorkbook.Headers[i];
        sheet.Cell(2, 1).Value = "Phân số";
        sheet.Cell(2, 2).Value = "Nhận biết";
        sheet.Cell(2, 4).Value = 2;
        sheet.Cell(2, 5).Value = 100;
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static MatrixImportService Service(References references) => new(references, new CurrentUser());
    private sealed class CurrentUser : IMatrixCurrentUser { public MatrixActor Actor => new(1, MatrixActorRole.Pht, 100); }
    private sealed class References : IMatrixReferenceRepository
    {
        public MatrixLessonOption[] Lessons = [new(1, 10, 11, "Phân số", 1, "1", "I", "Số")];
        public bool Forbidden;
        public int Reads;
        public MatrixActor Actor;
        public Task<MatrixReferenceModel> GetReferenceDataAsync(MatrixActor actor, ulong? id, CancellationToken ct)
        {
            Actor = actor;
            Reads++;
            if (Forbidden) throw new MatrixDomainException("Forbidden", "Ngoài chi nhánh");
            return Task.FromResult(new MatrixReferenceModel([new(ScopeKey, "Toán 5", 30, 100, 40, 50)],
                [new(20, 30, "HK I", null, null)], Lessons, []));
        }
        public Task EnsureValidAsync(MatrixScope scope, IReadOnlyCollection<ulong> lessons, CancellationToken ct, ulong? branch = null) => throw new NotSupportedException();
        public Task<MatrixExportInfo> GetExportInfoAsync(MatrixScope scope, IReadOnlyCollection<ulong> lessons, CancellationToken ct) => throw new NotSupportedException();
        public Task EnsureAssignmentValidAsync(MatrixActor actor, ulong user, MatrixScope scope, CancellationToken ct) => throw new NotSupportedException();
    }
}
