using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using ClosedXML.Excel;
using Domain.Entities.Identity;
using Infrastructure.Exports;
using Infrastructure.Repositories.Interface;

namespace Application.Tests.SchoolDirectory;

public sealed class StudentImportServiceTests
{
    private static readonly ImportCaller PrincipalA = ImportCaller.School(5);
    private static readonly ImportCaller PrincipalB = ImportCaller.School(6);
    private static readonly ImportCaller AdminForA = ImportCaller.Admin(1, FakeStudentImportRepository.SchoolA);
    private static readonly ImportCaller AdminForB = ImportCaller.Admin(1, FakeStudentImportRepository.SchoolB);

    private static (StudentImportService Service, FakeStudentImportRepository Repository) Create()
    {
        var repository = new FakeStudentImportRepository();
        return (new StudentImportService(repository, TimeProvider.System), repository);
    }

    // ---- template ----

    [Fact]
    public async Task TemplateInfo_ListsColumnsFormatsAndTheSchoolsLiveClasses()
    {
        var (service, _) = Create();

        var result = await service.GetTemplateInfoAsync(PrincipalA, null, CancellationToken.None);

        var info = result.Value!;
        Assert.Equal(6, info.Columns.Count);
        Assert.Equal(new[] { "6A", "6B" }, info.Classes.Select(c => c.Code));
        Assert.Equal("dd/MM/yyyy", info.DateFormat);
        Assert.Equal(StudentImportWorkbook.MaxRows, info.MaxRows);
        Assert.Equal(StudentImportService.MaxFileBytes, info.MaxFileBytes);
    }

    [Fact]
    public async Task TemplateDownload_IsAnXlsxNamedAfterTheAcademicYear()
    {
        var (service, _) = Create();

        var result = await service.BuildTemplateAsync(PrincipalA, null, CancellationToken.None);

        Assert.EndsWith(".xlsx", result.Value!.FileName);
        Assert.Contains("2025-2026", result.Value.FileName);
        using var workbook = new XLWorkbook(new MemoryStream(result.Value.Content));
        Assert.NotNull(workbook.Worksheet(StudentImportWorkbook.DataSheet));
    }

    [Fact]
    public async Task Template_RejectsAYearThatIsNotTheSchoolsAndAnActorWithoutASchool()
    {
        var (service, _) = Create();

        var badYear = await service.GetTemplateInfoAsync(PrincipalA, 999, CancellationToken.None);
        var zeroYear = await service.GetTemplateInfoAsync(PrincipalA, 0, CancellationToken.None);
        var noSchool = await service.GetTemplateInfoAsync(
            ImportCaller.School(999), null, CancellationToken.None);
        var unknown = await service.GetTemplateInfoAsync(
            ImportCaller.School(12345), null, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, badYear.Error!.Code);
        Assert.Contains("academicYearId", badYear.Error.Details!.Keys);
        Assert.Equal(SchoolDirectoryErrorCodes.Validation, zeroYear.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.SchoolScopeRequired, noSchool.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.Unauthorized, unknown.Error!.Code);
    }

    // ---- preview / validation ----

    [Fact]
    public async Task Preview_StoresAValidFileAsADraftWithTheCallersSource()
    {
        var (service, repository) = Create();
        var file = Workbook(
            Row(2, "HS001", "Nguyễn Văn An", "15/03/2014", "NAM", "05/09/2025", "6A"),
            Row(3, "HS002", "Trần Thị Bình", "", "", "05/09/2025", "6b"));

        var school = await service.PreviewAsync(PrincipalA, null, "ds.xlsx", file, CancellationToken.None);
        var admin = await service.PreviewAsync(AdminForA, null, "ds.xlsx", file, CancellationToken.None);

        var batch = school.Value!.Batch;
        Assert.Equal(StudentImportBatchStatusCodes.Draft, batch.Status);
        Assert.Equal(StudentImportSourceCodes.School, batch.Source);
        Assert.Equal((2, 2, 0), (batch.TotalRows, batch.ValidRows, batch.InvalidRows));
        Assert.All(school.Value.Rows.Items, r => Assert.True(r.IsValid));
        Assert.Equal(new ulong?[] { 101, 102 }, school.Value.Rows.Items.Select(r => r.ClassId));
        Assert.Equal(StudentImportSourceCodes.Admin, admin.Value!.Batch.Source);
        Assert.Equal(2, repository.SavedBatches);
    }

    [Fact]
    public async Task Preview_ReportsEveryFieldErrorOnItsOwnRowWithoutStoppingAtTheFirst()
    {
        var (service, _) = Create();
        var file = Workbook(
            Row(2, "", "", "31/02/2014", "ROBOT", "", ""),
            Row(3, "HS002", "An", "01/01/2999", "NAM", "05/09/2025", "9Z"),
            Row(4, "HS003", "Bình", "", "", "khong phai ngay", "6A"),
            Row(5, new string('x', 65), new string('n', 256), "", "", "05/09/2025", "6A"));

        var result = await service.PreviewAsync(PrincipalA, null, "ds.xlsx", file, CancellationToken.None);

        var rows = result.Value!.Rows.Items;
        Assert.Equal(4, result.Value.Batch.InvalidRows);
        Assert.Equal(
            new[] { "admissionDate", "classCode", "code", "dateOfBirth", "fullName", "gender" },
            Fields(rows[0]));
        Assert.Equal(new[] { "classCode", "dateOfBirth" }, Fields(rows[1]));
        Assert.Equal(new[] { "admissionDate" }, Fields(rows[2]));
        Assert.Equal(new[] { "code", "fullName" }, Fields(rows[3]));
        Assert.All(rows, r => Assert.False(r.IsValid));
    }

    [Fact]
    public async Task Preview_FlagsTheSecondOccurrenceOfACodeAndCodesAlreadyInTheSystem()
    {
        var (service, repository) = Create();
        repository.ExistingCodes.Add("TAKEN1");
        var file = Workbook(
            Row(2, "HS001", "An", "", "", "05/09/2025", "6A"),
            Row(3, "hs001", "Bình", "", "", "05/09/2025", "6A"),
            Row(4, "TAKEN1", "Cường", "", "", "05/09/2025", "6A"));

        var result = await service.PreviewAsync(PrincipalA, null, "ds.xlsx", file, CancellationToken.None);

        var rows = result.Value!.Rows.Items;
        Assert.True(rows[0].IsValid);
        Assert.False(rows[1].IsValid);
        Assert.Contains("dòng 2", rows[1].Errors.Single().Message);
        Assert.False(rows[2].IsValid);
        Assert.Contains("đã tồn tại", rows[2].Errors.Single().Message);
    }

    [Fact]
    public async Task Preview_RejectsAClassCodeSharedByTwoBranches()
    {
        var (service, repository) = Create();
        repository.Classes =
        [
            new(101, "6A", "Lớp 6A cơ sở 1"),
            new(201, "6A", "Lớp 6A cơ sở 2")
        ];
        var file = Workbook(Row(2, "HS001", "An", "", "", "05/09/2025", "6A"));

        var result = await service.PreviewAsync(PrincipalA, null, "ds.xlsx", file, CancellationToken.None);

        var row = result.Value!.Rows.Items.Single();
        Assert.False(row.IsValid);
        Assert.Contains("nhiều cơ sở", row.Errors.Single().Message);
    }

    [Fact]
    public async Task Preview_KeepsTheRawCellsAndAcceptsGenderSpellings()
    {
        var (service, _) = Create();
        var file = Workbook(
            Row(2, "HS001", "An", "15/03/2014", "Nữ", "5/9/2025", "6A"),
            Row(3, "HS002", "Bình", "2014-03-15", "khác", "2025-09-05", "6A"));

        var result = await service.PreviewAsync(PrincipalA, null, "ds.xlsx", file, CancellationToken.None);

        var rows = result.Value!.Rows.Items;
        Assert.All(rows, r => Assert.True(r.IsValid));
        Assert.Equal("Nữ", rows[0].Gender);
        Assert.Equal("5/9/2025", rows[0].AdmissionDate);
    }

    [Theory]
    [InlineData("ds.xls")]
    [InlineData("ds.csv")]
    [InlineData("")]
    public async Task Preview_RejectsAFileThatIsNotNamedXlsx(string name)
    {
        var (service, repository) = Create();

        var result = await service.PreviewAsync(
            PrincipalA, null, name, Workbook(Row(2, "HS1", "An", "", "", "05/09/2025", "6A")),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportFileInvalid, result.Error!.Code);
        Assert.Equal(0, repository.SavedBatches);
    }

    [Fact]
    public async Task Preview_RejectsEmptyOversizeAndNonZipContent()
    {
        var (service, repository) = Create();

        var empty = await service.PreviewAsync(PrincipalA, null, "a.xlsx", [], CancellationToken.None);
        var big = await service.PreviewAsync(
            PrincipalA, null, "a.xlsx", new byte[StudentImportService.MaxFileBytes + 1],
            CancellationToken.None);
        var text = await service.PreviewAsync(
            PrincipalA, null, "a.xlsx", "MZ this is not a spreadsheet"u8.ToArray(),
            CancellationToken.None);
        var noRows = await service.PreviewAsync(
            PrincipalA, null, "a.xlsx", Workbook(), CancellationToken.None);

        Assert.All(new[] { empty, big, text, noRows },
            r => Assert.Equal(SchoolDirectoryErrorCodes.ImportFileInvalid, r.Error!.Code));
        Assert.Equal(0, repository.SavedBatches);
    }

    // ---- school isolation ----

    [Fact]
    public async Task ABatchOfAnotherSchoolLooksLikeItDoesNotExist()
    {
        var (service, _) = Create();
        var id = await UploadValid(service, PrincipalA);

        var other = await service.GetAsync(PrincipalB, id, new StudentImportRowsQuery(), CancellationToken.None);
        var file = await service.GetFileAsync(PrincipalB, id, CancellationToken.None);
        var submit = await service.SubmitAsync(PrincipalB, id, CancellationToken.None);
        var wrongRoute = await service.GetAsync(AdminForB, id, new StudentImportRowsQuery(), CancellationToken.None);
        var missing = await service.GetAsync(PrincipalA, 999999, new StudentImportRowsQuery(), CancellationToken.None);

        Assert.All(new[] { other.Error, submit.Error, wrongRoute.Error, missing.Error },
            e => Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchNotFound, e!.Code));
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchNotFound, file.Error!.Code);
    }

    [Fact]
    public async Task Lists_AreScopedToTheSchoolAndTheAdminInboxSeesEverySchool()
    {
        var (service, _) = Create();
        await UploadValid(service, PrincipalA);
        await UploadValid(service, PrincipalA);
        await UploadValid(service, PrincipalB);

        var a = await service.ListAsync(PrincipalA, new StudentImportListQuery(null), CancellationToken.None);
        var b = await service.ListAsync(PrincipalB, new StudentImportListQuery(null), CancellationToken.None);
        var inbox = await service.ListAsync(
            ImportCaller.Admin(1, null), new StudentImportListQuery(null), CancellationToken.None);
        var forA = await service.ListAsync(AdminForA, new StudentImportListQuery(null), CancellationToken.None);

        Assert.Equal(2, a.Value!.TotalCount);
        Assert.Equal(1, b.Value!.TotalCount);
        Assert.Equal(3, inbox.Value!.TotalCount);
        Assert.Equal(2, forA.Value!.TotalCount);
    }

    [Fact]
    public async Task List_ValidatesStatusAndPaging()
    {
        var (service, _) = Create();

        var badStatus = await service.ListAsync(
            PrincipalA, new StudentImportListQuery("BOGUS"), CancellationToken.None);
        var badPaging = await service.ListAsync(
            PrincipalA, new StudentImportListQuery(null, 0, 101), CancellationToken.None);

        Assert.Contains("status", badStatus.Error!.Details!.Keys);
        Assert.Equal(new[] { "page", "pageSize" }, badPaging.Error!.Details!.Keys.Order());
    }

    // ---- submit / cancel ----

    [Fact]
    public async Task School_SubmitsAValidDraftAndItBecomesSubmitted()
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, PrincipalA);

        var result = await service.SubmitAsync(PrincipalA, id, CancellationToken.None);

        Assert.Equal(StudentImportBatchStatusCodes.Submitted, result.Value!.Batch.Status);
        Assert.Equal(StudentImportBatchStatusCodes.Submitted, repository.StatusOf(id));
    }

    [Fact]
    public async Task School_CannotSubmitABatchWithInvalidRows()
    {
        var (service, repository) = Create();
        var id = await Upload(service, PrincipalA, Row(2, "", "An", "", "", "05/09/2025", "6A"));

        var result = await service.SubmitAsync(PrincipalA, id, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportHasInvalidRows, result.Error!.Code);
        Assert.Equal(StudentImportBatchStatusCodes.Draft, repository.StatusOf(id));
    }

    [Fact]
    public async Task SubmitIsRefusedTwiceAndForAdminSourceBatchesAndForAdmins()
    {
        var (service, _) = Create();
        var schoolBatch = await UploadValid(service, PrincipalA);
        var adminBatch = await UploadValid(service, AdminForA);

        var first = await service.SubmitAsync(PrincipalA, schoolBatch, CancellationToken.None);
        var second = await service.SubmitAsync(PrincipalA, schoolBatch, CancellationToken.None);
        var adminSource = await service.SubmitAsync(PrincipalA, adminBatch, CancellationToken.None);
        var byAdmin = await service.SubmitAsync(AdminForA, schoolBatch, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.All(new[] { second.Error, adminSource.Error, byAdmin.Error },
            e => Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, e!.Code));
    }

    [Fact]
    public async Task OnlyDraftsCanBeCancelled()
    {
        var (service, repository) = Create();
        var draft = await UploadValid(service, PrincipalA);
        var submitted = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, submitted, CancellationToken.None);

        var cancelled = await service.CancelAsync(PrincipalA, draft, CancellationToken.None);
        var refused = await service.CancelAsync(PrincipalA, submitted, CancellationToken.None);
        var again = await service.CancelAsync(PrincipalA, draft, CancellationToken.None);

        Assert.Equal(StudentImportBatchStatusCodes.Cancelled, cancelled.Value!.Batch.Status);
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, refused.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, again.Error!.Code);
        Assert.Equal(StudentImportBatchStatusCodes.Submitted, repository.StatusOf(submitted));
    }

    // ---- apply ----

    [Fact]
    public async Task Admin_AppliesItsOwnDraftDirectlyAndTheStudentsCarryParsedValues()
    {
        var (service, repository) = Create();
        var id = await Upload(service, AdminForA,
            Row(2, "HS001", "Nguyễn Văn An", "15/03/2014", "Nam", "05/09/2025", "6A"),
            Row(3, "HS002", "Trần Thị Bình", "", "", "05/09/2025", "6B"));

        var result = await service.ApplyAsync(AdminForA, id, CancellationToken.None);

        Assert.Equal(StudentImportBatchStatusCodes.Applied, result.Value!.Batch.Status);
        Assert.Equal(2, repository.LastApplied!.Count);
        var first = repository.LastApplied[0];
        Assert.Equal(
            ("HS001", "Nguyễn Văn An", new DateOnly(2014, 3, 15), "NAM", new DateOnly(2025, 9, 5), 101ul),
            (first.Code, first.FullName, first.DateOfBirth, first.Gender, first.AdmissionDate, first.SchoolClassId));
        Assert.Null(repository.LastApplied[1].DateOfBirth);
        Assert.All(result.Value.Rows.Items, r => Assert.NotNull(r.CreatedStudentId));
    }

    [Fact]
    public async Task Admin_AppliesASubmittedSchoolBatchButNotAnUnsubmittedOne()
    {
        var (service, _) = Create();
        var unsubmitted = await UploadValid(service, PrincipalA);
        var submitted = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, submitted, CancellationToken.None);

        var refused = await service.ApplyAsync(AdminForA, unsubmitted, CancellationToken.None);
        var applied = await service.ApplyAsync(AdminForA, submitted, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, refused.Error!.Code);
        Assert.Equal(StudentImportBatchStatusCodes.Applied, applied.Value!.Batch.Status);
    }

    [Fact]
    public async Task ApplyingTwiceWritesOnlyOnce()
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, AdminForA);

        var first = await service.ApplyAsync(AdminForA, id, CancellationToken.None);
        var second = await service.ApplyAsync(AdminForA, id, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, second.Error!.Code);
        Assert.Equal(1, repository.ApplyCalls);
    }

    [Fact]
    public async Task ASchoolCallerCanNeverApply()
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, id, CancellationToken.None);

        var result = await service.ApplyAsync(PrincipalA, id, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, result.Error!.Code);
        Assert.Equal(0, repository.ApplyCalls);
        Assert.Equal(StudentImportBatchStatusCodes.Submitted, repository.StatusOf(id));
    }

    [Fact]
    public async Task ABatchWithInvalidRowsCannotBeApplied()
    {
        var (service, repository) = Create();
        var id = await Upload(service, AdminForA,
            Row(2, "HS001", "An", "", "", "05/09/2025", "6A"),
            Row(3, "", "Bình", "", "", "05/09/2025", "6A"));

        var result = await service.ApplyAsync(AdminForA, id, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportHasInvalidRows, result.Error!.Code);
        Assert.Equal(0, repository.ApplyCalls);
        Assert.Equal(StudentImportBatchStatusCodes.Draft, repository.StatusOf(id));
    }

    [Fact]
    public async Task RowsThatBecameInvalidBeforeApplyAreReportedAndNothingIsWritten()
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, AdminForA);
        repository.ApplyConflicts =
        [
            new ImportRowConflict(2, "HS001", "CODE_TAKEN"),
            new ImportRowConflict(3, "HS002", "CLASS_UNAVAILABLE")
        ];

        var result = await service.ApplyAsync(AdminForA, id, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ImportHasInvalidRows, result.Error!.Code);
        var messages = result.Error.Details!["rows"];
        Assert.Contains("Dòng 2", messages[0]);
        Assert.Contains("HS001", messages[0]);
        Assert.Contains("Dòng 3", messages[1]);
        Assert.Equal(StudentImportBatchStatusCodes.Draft, repository.StatusOf(id));
    }

    // ---- reject ----

    [Fact]
    public async Task Admin_RejectsASubmittedBatchWithAReasonTheSchoolCanRead()
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, id, CancellationToken.None);

        var result = await service.RejectAsync(
            AdminForA, id, new RejectStudentImportRequest("  Sai mã lớp  "), CancellationToken.None);
        var seenBySchool = await service.GetAsync(
            PrincipalA, id, new StudentImportRowsQuery(), CancellationToken.None);

        Assert.Equal(StudentImportBatchStatusCodes.Rejected, result.Value!.Batch.Status);
        Assert.Equal("Sai mã lớp", repository.CommentOf(id));
        Assert.Equal(1ul, repository.ReviewerOf(id));
        Assert.Equal("Sai mã lớp", seenBySchool.Value!.Batch.ReviewComment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Reject_RequiresAReason(string? comment)
    {
        var (service, repository) = Create();
        var id = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, id, CancellationToken.None);

        var result = await service.RejectAsync(
            AdminForA, id, new RejectStudentImportRequest(comment), CancellationToken.None);

        Assert.Contains("comment", result.Error!.Details!.Keys);
        Assert.Equal(StudentImportBatchStatusCodes.Submitted, repository.StatusOf(id));
    }

    [Fact]
    public async Task Reject_IsRefusedForTooLongReasonsDraftsAndSchoolCallers()
    {
        var (service, repository) = Create();
        var draft = await UploadValid(service, PrincipalA);
        var submitted = await UploadValid(service, PrincipalA);
        await service.SubmitAsync(PrincipalA, submitted, CancellationToken.None);

        var tooLong = await service.RejectAsync(
            AdminForA, submitted, new RejectStudentImportRequest(new string('x', 1001)),
            CancellationToken.None);
        var onDraft = await service.RejectAsync(
            AdminForA, draft, new RejectStudentImportRequest("no"), CancellationToken.None);
        var bySchool = await service.RejectAsync(
            PrincipalA, submitted, new RejectStudentImportRequest("no"), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, tooLong.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, onDraft.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.ImportBatchStateInvalid, bySchool.Error!.Code);
        Assert.Equal(StudentImportBatchStatusCodes.Submitted, repository.StatusOf(submitted));
    }

    [Fact]
    public async Task OriginalFileComesBackAsAnXlsx()
    {
        var (service, _) = Create();
        var content = Workbook(Row(2, "HS001", "An", "", "", "05/09/2025", "6A"));
        var preview = await service.PreviewAsync(
            PrincipalA, null, "C:\\fakepath\\Danh sach:lop6.xlsx", content, CancellationToken.None);

        var file = await service.GetFileAsync(
            PrincipalA, preview.Value!.Batch.Id, CancellationToken.None);

        Assert.Equal(content, file.Value!.Content);
        Assert.EndsWith(".xlsx", file.Value.FileName);
        Assert.DoesNotContain(':', file.Value.FileName);
        Assert.DoesNotContain('\\', file.Value.FileName);
    }

    // ---- helpers ----

    private static async Task<ulong> UploadValid(StudentImportService service, ImportCaller caller) =>
        await Upload(service, caller,
            Row(2, "HS001", "Nguyễn Văn An", "15/03/2014", "NAM", "05/09/2025", "6A"),
            Row(3, "HS002", "Trần Thị Bình", "", "", "05/09/2025", "6B"));

    private static async Task<ulong> Upload(
        StudentImportService service, ImportCaller caller, params SheetRow[] rows)
    {
        var result = await service.PreviewAsync(
            caller, null, "ds.xlsx", Workbook(rows), CancellationToken.None);
        return result.Value!.Batch.Id;
    }

    private static string[] Fields(StudentImportRowDto row) =>
        row.Errors.Select(e => e.Field).Order().ToArray();

    private sealed record SheetRow(
        int Number, string Code, string Name, string Dob, string Gender, string Admission, string Class);

    private static SheetRow Row(
        int number, string code, string name, string dob, string gender, string admission, string classCode) =>
        new(number, code, name, dob, gender, admission, classCode);

    private static byte[] Workbook(params SheetRow[] rows)
    {
        using var workbook = new XLWorkbook(new MemoryStream(
            StudentImportWorkbook.CreateTemplate("2025-2026", [])));
        var sheet = workbook.Worksheet(StudentImportWorkbook.DataSheet);
        foreach (var row in rows)
        {
            sheet.Cell(row.Number, 1).SetValue(row.Code);
            sheet.Cell(row.Number, 2).SetValue(row.Name);
            sheet.Cell(row.Number, 3).SetValue(row.Dob);
            sheet.Cell(row.Number, 4).SetValue(row.Gender);
            sheet.Cell(row.Number, 5).SetValue(row.Admission);
            sheet.Cell(row.Number, 6).SetValue(row.Class);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
