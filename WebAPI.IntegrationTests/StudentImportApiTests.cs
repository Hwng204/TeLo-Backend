using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WebAPI.IntegrationTests;

public sealed class StudentImportApiTests : IClassFixture<TestApplicationFactory>
{
    private const ulong SchoolId = 5;
    private const string AdminRole = "OperationalAdmin";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly WebApplicationFactory<WebAPI.Controllers.StudentsController> factory;
    private readonly FakeStudentImportService service = new();

    public StudentImportApiTests(TestApplicationFactory factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStudentImportService>();
                services.AddSingleton<IStudentImportService>(service);
            }));
    }

    private HttpClient Client(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    // ---- who may import ----

    [Theory]
    [InlineData("HIEU_TRUONG")]
    [InlineData("PRINCIPAL")]
    [InlineData("PHT")]
    public async Task PrincipalsAndVicePrincipals_UseTheSchoolImportRoutes(string role)
    {
        using var client = Client(role);

        using var info = await client.GetAsync("/api/student-imports/template");
        using var file = await client.GetAsync("/api/student-imports/template.xlsx");
        using var list = await client.GetAsync("/api/student-imports");
        using var upload = await client.PostAsync("/api/student-imports", Upload("ds.xlsx", [1, 2, 3]));

        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
    }

    [Theory]
    [InlineData("GIAO_VIEN")]
    [InlineData("TEACHER")]
    [InlineData("TEAM_LEAD")]
    [InlineData("TO_TRUONG")]
    [InlineData("STUDENT")]
    [InlineData(AdminRole)]
    public async Task TeachersTeamLeadsAndAdminsCannotUseTheSchoolImportRoutes(string role)
    {
        using var client = Client(role);

        using var info = await client.GetAsync("/api/student-imports/template");
        using var file = await client.GetAsync("/api/student-imports/template.xlsx");
        using var upload = await client.PostAsync("/api/student-imports", Upload("ds.xlsx", [1]));
        using var submit = await client.PostAsync("/api/student-imports/7/submit", null);
        using var cancel = await client.DeleteAsync("/api/student-imports/7");

        Assert.All(new[] { info, file, upload, submit, cancel },
            r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(0, service.Calls);
    }

    [Theory]
    [InlineData("PHT")]
    [InlineData("HIEU_TRUONG")]
    [InlineData("TEACHER")]
    public async Task SchoolRolesCannotReachTheAdminImportRoutes(string role)
    {
        using var client = Client(role);
        var root = $"/api/admin/schools/{SchoolId}/student-imports";

        using var list = await client.GetAsync(root);
        using var inbox = await client.GetAsync("/api/admin/student-imports");
        using var upload = await client.PostAsync(root, Upload("ds.xlsx", [1]));
        using var apply = await client.PostAsync($"{root}/7/apply", null);
        using var reject = await client.PostAsJsonAsync(
            $"{root}/7/reject", new RejectStudentImportRequest("no"));

        Assert.All(new[] { list, inbox, upload, apply, reject },
            r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(0, service.Calls);
    }

    [Theory]
    [InlineData("/api/student-imports")]
    [InlineData("/api/student-imports/template.xlsx")]
    [InlineData("/api/admin/student-imports")]
    [InlineData("/api/admin/schools/5/student-imports")]
    public async Task ImportRoutesRequireAuthentication(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminUsesTheSchoolNamedInTheRouteAndActsAsAdmin()
    {
        using var client = Client(AdminRole);
        var root = $"/api/admin/schools/{SchoolId}/student-imports";

        using var response = await client.PostAsync(root, Upload("ds.xlsx", [1, 2]));
        using var applied = await client.PostAsync($"{root}/7/apply", null);
        using var inbox = await client.GetAsync("/api/admin/student-imports?status=SUBMITTED");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
        var upload = service.Recorded.First(c => c.Name == "Preview");
        Assert.True(upload.Caller.IsAdmin);
        Assert.Equal(SchoolId, upload.Caller.SchoolId);
        var listing = service.Recorded.Single(c => c.Name == "List");
        Assert.True(listing.Caller.IsAdmin);
        Assert.Null(listing.Caller.SchoolId);
        Assert.Equal("SUBMITTED", listing.Detail);
    }

    [Fact]
    public async Task SchoolCallersNeverSupplyASchoolTheServiceResolvesItFromTheUser()
    {
        using var client = Client("PHT");

        using var response = await client.PostAsync("/api/student-imports", Upload("ds.xlsx", [1]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var caller = service.Recorded.Single(c => c.Name == "Preview").Caller;
        Assert.False(caller.IsAdmin);
        Assert.Null(caller.SchoolId);
        Assert.Equal(10ul, caller.ActorUserId);
    }

    // ---- template ----

    [Fact]
    public async Task TemplateDownload_IsASpreadsheetAttachment()
    {
        using var client = Client("PHT");

        using var response = await client.GetAsync("/api/student-imports/template.xlsx?academicYearId=3");

        Assert.Equal(XlsxType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("MauDanhSachHocSinh", response.Content.Headers.ContentDisposition.FileName);
        Assert.Equal("3", service.Recorded.Single(c => c.Name == "BuildTemplate").Detail);
    }

    [Fact]
    public async Task TemplateInfo_IsJsonAndNotCapturedByTheBatchIdRoute()
    {
        using var client = Client("HIEU_TRUONG");

        var body = await client.GetStringAsync("/api/student-imports/template");

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(6, data.GetProperty("columns").GetArrayLength());
        Assert.Equal("dd/MM/yyyy", data.GetProperty("dateFormat").GetString());
    }

    // ---- upload ----

    [Fact]
    public async Task Upload_BindsTheMultipartFileAndReturnsThePreview()
    {
        using var client = Client("PHT");

        using var response = await client.PostAsync(
            "/api/student-imports?academicYearId=9", Upload("Danh sách 6A.xlsx", [9, 8, 7, 6]));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = service.Recorded.Single(c => c.Name == "Preview");
        Assert.Equal("Danh sách 6A.xlsx", call.Detail);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, service.LastContent);
        Assert.Equal(9ul, service.LastAcademicYearId);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("DRAFT", json.RootElement.GetProperty("data").GetProperty("batch")
            .GetProperty("status").GetString());
    }

    [Fact]
    public async Task Upload_WithoutAFileIsAValidationError()
    {
        using var client = Client("PHT");

        // A well-formed form that simply has no "file" part.
        using var response = await client.PostAsync(
            "/api/student-imports",
            new MultipartFormDataContent { { new StringContent("x"), "note" } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("VALIDATION_ERROR", body);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task Upload_WithAMalformedBodyIsABindingErrorNotAValidationError()
    {
        using var client = Client("PHT");

        // No parts at all is not a form, so it is rejected by model binding (400), like bad JSON.
        using var response = await client.PostAsync(
            "/api/student-imports", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task Upload_ALargeFileIsRefusedBeforeItIsReadIntoMemory()
    {
        using var client = Client("PHT");

        // Over the service limit (2 MB) but inside the request cap, so the controller answers.
        using var response = await client.PostAsync(
            "/api/student-imports",
            Upload("big.xlsx", new byte[StudentImportService.MaxFileBytes + 1024]));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("IMPORT_FILE_INVALID", body);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task Upload_AGrosslyOversizedBodyNeverReachesTheService()
    {
        using var client = Client("PHT");

        using var response = await client.PostAsync(
            "/api/student-imports", Upload("huge.xlsx", new byte[4 * 1024 * 1024]));

        // Kestrel's RequestSizeLimit rejects this with 400 "Request body too large" (MVC reads the
        // form while binding, so it is not a 413) - confirmed against the real server. TestServer does
        // not enforce that cap, so here the controller's own size check answers 422 instead. Either way
        // the body is refused and never reaches the service.
        Assert.True(
            response.StatusCode is HttpStatusCode.RequestEntityTooLarge
                or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"Expected the body to be refused, got {(int)response.StatusCode}.");
        Assert.Equal(0, service.Calls);
    }

    // ---- results and errors ----

    [Theory]
    [InlineData(404, HttpStatusCode.NotFound, "IMPORT_BATCH_NOT_FOUND")]
    [InlineData(409, HttpStatusCode.Conflict, "IMPORT_BATCH_STATE_INVALID")]
    [InlineData(410, HttpStatusCode.Conflict, "IMPORT_HAS_INVALID_ROWS")]
    [InlineData(422, HttpStatusCode.UnprocessableEntity, "IMPORT_FILE_INVALID")]
    public async Task ServiceFailuresMapToStableStatusCodes(
        int batchId, HttpStatusCode expected, string code)
    {
        using var client = Client("PHT");

        using var response = await client.PostAsync($"/api/student-imports/{batchId}/submit", null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expected, response.StatusCode);
        Assert.Contains(code, body);
    }

    [Fact]
    public async Task Reject_PassesTheReasonToTheService()
    {
        using var client = Client(AdminRole);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/student-imports/7/reject",
            new RejectStudentImportRequest("Sai mã lớp"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sai mã lớp", service.Recorded.Single(c => c.Name == "Reject").Detail);
    }

    [Fact]
    public async Task OriginalFileDownload_IsAnAttachmentForBothSides()
    {
        using var school = Client("PHT");
        using var admin = Client(AdminRole);

        using var fromSchool = await school.GetAsync("/api/student-imports/7/file.xlsx");
        using var fromAdmin = await admin.GetAsync(
            $"/api/admin/schools/{SchoolId}/student-imports/7/file.xlsx");

        Assert.All(new[] { fromSchool, fromAdmin }, r =>
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(XlsxType, r.Content.Headers.ContentType!.MediaType);
        });
    }

    [Fact]
    public async Task Details_AcceptRowPagingAndAnOnlyInvalidFilter()
    {
        using var client = Client("PHT");

        using var response = await client.GetAsync(
            "/api/student-imports/7?page=2&pageSize=50&onlyInvalid=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2/50/True", service.Recorded.Single(c => c.Name == "Get").Detail);
    }

    private static MultipartFormDataContent Upload(string fileName, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private sealed record Call(string Name, ImportCaller Caller, string? Detail);

    private sealed class FakeStudentImportService : IStudentImportService
    {
        private readonly List<Call> recorded = [];

        public IReadOnlyList<Call> Recorded => recorded;

        public int Calls => recorded.Count;

        public byte[]? LastContent { get; private set; }

        public ulong? LastAcademicYearId { get; private set; }

        public Task<ServiceResult<StudentImportTemplateInfo>> GetTemplateInfoAsync(
            ImportCaller caller, ulong? academicYearId, CancellationToken cancellationToken)
        {
            Record("TemplateInfo", caller, academicYearId?.ToString());
            return Task.FromResult(ServiceResult<StudentImportTemplateInfo>.Success(
                new StudentImportTemplateInfo(
                    1, "2025-2026",
                    Enumerable.Range(0, 6)
                        .Select(i => new StudentImportColumnDto($"c{i}", $"Cột {i}", i % 2 == 0, "text", "x"))
                        .ToList(),
                    [new DirectoryOption(1, "6A", "Lớp 6A")], ["NAM", "NU", "KHAC"],
                    "dd/MM/yyyy", 1000, 2 * 1024 * 1024)));
        }

        public Task<ServiceResult<StudentImportFile>> BuildTemplateAsync(
            ImportCaller caller, ulong? academicYearId, CancellationToken cancellationToken)
        {
            Record("BuildTemplate", caller, academicYearId?.ToString());
            return Task.FromResult(ServiceResult<StudentImportFile>.Success(
                new StudentImportFile("MauDanhSachHocSinh_2025-2026.xlsx", [1, 2, 3])));
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> PreviewAsync(
            ImportCaller caller, ulong? academicYearId, string fileName, byte[] content,
            CancellationToken cancellationToken)
        {
            Record("Preview", caller, fileName);
            LastContent = content;
            LastAcademicYearId = academicYearId;
            return Task.FromResult(ServiceResult<StudentImportBatchDetailDto>.Success(Detail(1, "DRAFT")));
        }

        public Task<ServiceResult<DirectoryPage<StudentImportBatchDto>>> ListAsync(
            ImportCaller caller, StudentImportListQuery query, CancellationToken cancellationToken)
        {
            Record("List", caller, query.Status);
            return Task.FromResult(ServiceResult<DirectoryPage<StudentImportBatchDto>>.Success(
                new DirectoryPage<StudentImportBatchDto>([Batch(1, "SUBMITTED")], 1, 20, 1)));
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> GetAsync(
            ImportCaller caller, ulong batchId, StudentImportRowsQuery rows,
            CancellationToken cancellationToken)
        {
            Record("Get", caller, $"{rows.Page}/{rows.PageSize}/{rows.OnlyInvalid}");
            return Task.FromResult(ServiceResult<StudentImportBatchDetailDto>.Success(Detail(batchId, "DRAFT")));
        }

        public Task<ServiceResult<StudentImportFile>> GetFileAsync(
            ImportCaller caller, ulong batchId, CancellationToken cancellationToken)
        {
            Record("File", caller, batchId.ToString());
            return Task.FromResult(ServiceResult<StudentImportFile>.Success(
                new StudentImportFile("ds.xlsx", [1, 2, 3])));
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> SubmitAsync(
            ImportCaller caller, ulong batchId, CancellationToken cancellationToken)
        {
            Record("Submit", caller, batchId.ToString());
            return Task.FromResult(batchId switch
            {
                404 => Fail(SchoolDirectoryErrorCodes.ImportBatchNotFound),
                409 => Fail(SchoolDirectoryErrorCodes.ImportBatchStateInvalid),
                410 => Fail(SchoolDirectoryErrorCodes.ImportHasInvalidRows),
                422 => Fail(SchoolDirectoryErrorCodes.ImportFileInvalid),
                _ => ServiceResult<StudentImportBatchDetailDto>.Success(Detail(batchId, "SUBMITTED"))
            });
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> CancelAsync(
            ImportCaller caller, ulong batchId, CancellationToken cancellationToken)
        {
            Record("Cancel", caller, batchId.ToString());
            return Task.FromResult(ServiceResult<StudentImportBatchDetailDto>.Success(
                Detail(batchId, "CANCELLED")));
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> ApplyAsync(
            ImportCaller caller, ulong batchId, CancellationToken cancellationToken)
        {
            Record("Apply", caller, batchId.ToString());
            return Task.FromResult(ServiceResult<StudentImportBatchDetailDto>.Success(
                Detail(batchId, "APPLIED")));
        }

        public Task<ServiceResult<StudentImportBatchDetailDto>> RejectAsync(
            ImportCaller caller, ulong batchId, RejectStudentImportRequest request,
            CancellationToken cancellationToken)
        {
            Record("Reject", caller, request.Comment);
            return Task.FromResult(ServiceResult<StudentImportBatchDetailDto>.Success(
                Detail(batchId, "REJECTED")));
        }

        private void Record(string name, ImportCaller caller, string? detail)
        {
            lock (recorded)
            {
                recorded.Add(new Call(name, caller, detail));
            }
        }

        private static ServiceResult<StudentImportBatchDetailDto> Fail(string code) =>
            ServiceResult<StudentImportBatchDetailDto>.Failure(code, "Lỗi mẫu.");

        private static StudentImportBatchDto Batch(ulong id, string status) => new(
            id, 5, "Trường 5", 1, "2025-2026", "SCHOOL", status, "ds.xlsx", 2, 2, 0, "Người tải",
            new DateTime(2025, 9, 1), null, null, null, null);

        private static StudentImportBatchDetailDto Detail(ulong id, string status) => new(
            Batch(id, status),
            new DirectoryPage<StudentImportRowDto>(
                [new StudentImportRowDto(2, "HS1", "An", "", "NAM", "05/09/2025", "6A", 1, "Lớp 6A", true, [], null)],
                1, 100, 1));
    }
}
