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
    private static readonly MatrixImportContext Context = new(10, 20, 10, "Toán");

    [Fact]
    public async Task Template_ContainsScopedLessonsAndThreeLevels()
    {
        var repository = new References();
        var file = await Service(repository).BuildTemplateAsync(Context, default);
        var sheet = MatrixImportWorkbook.Read(file.Content);
        Assert.Equal("mau-ma-tran-10.xlsx", file.FileName);
        Assert.Equal(11, sheet.Rows.Count);
        Assert.All(sheet.Rows.Skip(8), row => Assert.Contains("Bài 1. Phân số — Chương I. Số", row[0]));
        Assert.Equal(new MatrixActor(1, MatrixActorRole.Pht, 100), repository.Actor);
    }

    [Theory]
    [InlineData(0, 20, 10)]
    [InlineData(11, 20, 10)]
    [InlineData(10, 99, 10)]
    [InlineData(10, 20, 0)]
    public async Task InvalidContextOrSemester_IsRejected(int id, int semester, int score)
    {
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            Service(new References()).BuildTemplateAsync(new((ulong)id, (ulong)semester, score), default));
        Assert.Contains(error.Code, new[] { "InvalidRequest", "InvalidReference" });
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
            return Task.FromResult(new MatrixReferenceModel([new(10, "Toán 5", 30, 100, 40, 50)],
                [new(20, 30, "HK I", null, null)], Lessons, []));
        }
        public Task EnsureValidAsync(ulong id, ulong? semester, IReadOnlyCollection<ulong> lessons, CancellationToken ct, ulong? branch = null) => throw new NotSupportedException();
        public Task<MatrixExportInfo> GetExportInfoAsync(ulong id, ulong? semester, IReadOnlyCollection<ulong> lessons, CancellationToken ct) => throw new NotSupportedException();
        public Task EnsureAssignmentValidAsync(MatrixActor actor, ulong user, ulong context, ulong? semester, CancellationToken ct) => throw new NotSupportedException();
    }
}
