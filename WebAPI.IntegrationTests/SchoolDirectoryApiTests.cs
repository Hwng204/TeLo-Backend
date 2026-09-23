using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WebAPI.IntegrationTests;

public sealed class SchoolDirectoryApiTests : IClassFixture<TestApplicationFactory>
{
    private const ulong SchoolId = 5;
    private const string AdminRole = "OperationalAdmin";

    private readonly WebApplicationFactory<WebAPI.Controllers.StudentsController> factory;

    public SchoolDirectoryApiTests(TestApplicationFactory factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISchoolDirectoryService>();
                services.AddScoped<ISchoolDirectoryService, FakeSchoolDirectoryService>();
                services.RemoveAll<ISchoolDirectoryAdminService>();
                services.AddScoped<ISchoolDirectoryAdminService, FakeSchoolDirectoryAdminService>();
            }));
    }

    private HttpClient Client(string role = "PHT")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    [Theory]
    [InlineData("/api/students")]
    [InlineData("/api/students/7")]
    [InlineData("/api/students/7/scores?classId=3")]
    [InlineData("/api/classes")]
    [InlineData("/api/classes/3")]
    [InlineData("/api/classes/reference-data")]
    [InlineData("/api/admin/schools/5/students")]
    [InlineData("/api/admin/schools/5/classes")]
    public async Task Directory_RequiresAuthentication(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("HIEU_TRUONG")]
    [InlineData("PRINCIPAL")]
    [InlineData("PHT")]
    [InlineData("GIAO_VIEN")]
    [InlineData("TEACHER")]
    [InlineData("TEAM_LEAD")]
    [InlineData("TO_TRUONG")]
    public async Task EverySchoolRole_ReadsItsOwnSchoolDirectory(string role)
    {
        using var client = Client(role);

        using var students = await client.GetAsync("/api/students");
        using var classes = await client.GetAsync("/api/classes");

        Assert.Equal(HttpStatusCode.OK, students.StatusCode);
        Assert.Equal(HttpStatusCode.OK, classes.StatusCode);
    }

    [Theory]
    [InlineData("STUDENT")]
    [InlineData("PARENT")]
    [InlineData(AdminRole)]
    public async Task RolesOutsideTheSchoolPolicy_AreForbidden(string role)
    {
        using var client = Client(role);

        using var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PHT")]
    [InlineData("HIEU_TRUONG")]
    [InlineData("TEACHER")]
    public async Task SchoolRoles_CannotReachAdminRoutes(string role)
    {
        using var client = Client(role);

        using var read = await client.GetAsync($"/api/admin/schools/{SchoolId}/students");
        using var write = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/students", NewStudent());
        using var delete = await client.DeleteAsync($"/api/admin/schools/{SchoolId}/students/7");

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task StudentsList_ReturnsStudentIdForDetailLink()
    {
        using var client = Client();

        var body = await client.GetStringAsync("/api/students");

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(7, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ClassesList_ReturnsClassIdForDetailLink()
    {
        using var client = Client("PRINCIPAL");

        var body = await client.GetStringAsync("/api/classes");

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(3, item.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ClassDetailRoster_ReturnsStudentIdForEachRow()
    {
        using var client = Client();

        var body = await client.GetStringAsync("/api/classes/3?page=1&pageSize=10");

        using var json = JsonDocument.Parse(body);
        var row = json.RootElement.GetProperty("data").GetProperty("students")
            .GetProperty("items")[0];
        Assert.Equal(7, row.GetProperty("studentId").GetInt32());
    }

    [Fact]
    public async Task StudentScores_ReturnsPublishedResultsForTheSelectedClass()
    {
        using var client = Client("GIAO_VIEN");

        var body = await client.GetStringAsync("/api/students/7/scores?classId=3");

        using var json = JsonDocument.Parse(body);
        var item = json.RootElement.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("Toán", item.GetProperty("subjectName").GetString());
        Assert.NotNull(item.GetProperty("resultPublishedAt").GetString());
    }

    [Fact]
    public async Task StudentScores_ForAClassOutsideHistory_ReturnsNotFound()
    {
        using var client = Client();

        using var response = await client.GetAsync("/api/students/7/scores?classId=404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReferenceDataRoute_IsNotCapturedByClassIdRoute()
    {
        using var client = Client();

        using var response = await client.GetAsync("/api/classes/reference-data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DetailOutsideScope_ReturnsNotFound()
    {
        using var client = Client();

        using var student = await client.GetAsync("/api/students/404");
        using var schoolClass = await client.GetAsync("/api/classes/404");

        Assert.Equal(HttpStatusCode.NotFound, student.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, schoolClass.StatusCode);
    }

    [Fact]
    public async Task InvalidPage_Returns422WithApiResponseError()
    {
        using var client = Client();

        using var response = await client.GetAsync("/api/students?page=0");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(SchoolDirectoryErrorCodes.Validation, body);
    }

    [Fact]
    public async Task MissingSchoolScope_Returns403()
    {
        using var client = Client();

        using var response = await client.GetAsync("/api/students?search=noscope");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminReadsTheSchoolNamedInTheRoute()
    {
        using var client = Client(AdminRole);

        var body = await client.GetStringAsync($"/api/admin/schools/{SchoolId}/students");

        using var json = JsonDocument.Parse(body);
        Assert.Equal(
            (int)SchoolId,
            json.RootElement.GetProperty("data").GetProperty("items")[0]
                .GetProperty("schoolBranchId").GetInt32());
    }

    [Fact]
    public async Task AdminUnknownSchool_ReturnsNotFound()
    {
        using var client = Client(AdminRole);

        using var response = await client.GetAsync("/api/admin/schools/404/students");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminCreatesAndUpdatesStudents()
    {
        using var client = Client(AdminRole);

        using var created = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/students", NewStudent());
        using var updated = await client.PutAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/students/7",
            new UpdateStudentRequest(
                "HS-7", "Nguyen An", null, null, new DateOnly(2025, 9, 1), "ACTIVE", 3));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    public async Task AdminTransfersAStudentToAnotherClass()
    {
        using var client = Client(AdminRole);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/students/7/transfer-class",
            new TransferStudentClassRequest(3, new DateOnly(2025, 11, 1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("PHT")]
    [InlineData("HIEU_TRUONG")]
    [InlineData("GIAO_VIEN")]
    public async Task SchoolRoles_CannotTransferClasses(string role)
    {
        using var client = Client(role);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/students/7/transfer-class",
            new TransferStudentClassRequest(3, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminDeleteIsLogicalAndReturnsTheInactiveProfile()
    {
        using var client = Client(AdminRole);

        using var response = await client.DeleteAsync(
            $"/api/admin/schools/{SchoolId}/students/7");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(
            "INACTIVE", json.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task AdminCreatesClassesAndSeesConflictsAs409()
    {
        using var client = Client(AdminRole);

        using var created = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/classes", NewClass("C-1"));
        using var duplicate = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/classes", NewClass("DUPLICATE"));
        using var stillPopulated = await client.DeleteAsync(
            $"/api/admin/schools/{SchoolId}/classes/409");

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stillPopulated.StatusCode);
        Assert.Contains(
            SchoolDirectoryErrorCodes.ClassHasActiveStudents,
            await stillPopulated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdminWriteValidationFailures_Return422()
    {
        using var client = Client(AdminRole);

        using var response = await client.PostAsJsonAsync(
            $"/api/admin/schools/{SchoolId}/classes", NewClass("INVALID"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SchoolRoutes_ExposeNoWriteVerbs()
    {
        using var client = Client();

        using var post = await client.PostAsJsonAsync("/api/students", NewStudent());
        using var delete = await client.DeleteAsync("/api/classes/3");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    private static CreateStudentRequest NewStudent() => new(
        "HS-7", "Nguyen An", null, null, new DateOnly(2025, 9, 1), "ACTIVE", 3);

    private static CreateClassRequest NewClass(string code) => new(
        1, code, "6A", 2, 3, "ACTIVE", null);

    private sealed class FakeSchoolDirectoryService : ISchoolDirectoryService
    {
        public Task<ServiceResult<DirectoryPage<StudentListItem>>> ListStudentsAsync(
            DirectoryScope scope, StudentListQuery query, CancellationToken cancellationToken)
        {
            if (scope.SchoolId is 404)
            {
                return Task.FromResult(ServiceResult<DirectoryPage<StudentListItem>>.Failure(
                    SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."));
            }

            if (query.Page < 1)
            {
                return Task.FromResult(ServiceResult<DirectoryPage<StudentListItem>>.Failure(
                    SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ."));
            }

            if (query.Search == "noscope")
            {
                return Task.FromResult(ServiceResult<DirectoryPage<StudentListItem>>.Failure(
                    SchoolDirectoryErrorCodes.SchoolScopeRequired, "Thiếu phạm vi trường."));
            }

            var item = new StudentListItem(
                7, "HS7", "An", null, null, null, null, null, null,
                scope.SchoolId, "Cơ sở 1", "ACTIVE", new DateOnly(2024, 9, 1));
            return Task.FromResult(ServiceResult<DirectoryPage<StudentListItem>>.Success(
                new DirectoryPage<StudentListItem>([item], 1, 20, 1)));
        }

        public Task<ServiceResult<StudentDetailDto>> GetStudentAsync(
            DirectoryScope scope, ulong studentId, CancellationToken cancellationToken) =>
            Task.FromResult(Student(studentId, "ACTIVE"));

        public Task<ServiceResult<DirectoryPage<StudentScoreItem>>> GetStudentScoresAsync(
            DirectoryScope scope, ulong studentId, ulong classId, PageQuery page,
            CancellationToken cancellationToken)
        {
            if (classId == 404)
            {
                return Task.FromResult(ServiceResult<DirectoryPage<StudentScoreItem>>.Failure(
                    SchoolDirectoryErrorCodes.StudentNotFound, "Không tìm thấy học sinh."));
            }

            var score = new StudentScoreItem(
                1, 2, "Thi HK1", 3, "HK1", 4, "Toán",
                new DateOnly(2025, 12, 1), 8.5m, new DateTime(2025, 12, 5));
            return Task.FromResult(ServiceResult<DirectoryPage<StudentScoreItem>>.Success(
                new DirectoryPage<StudentScoreItem>([score], page.Page, page.PageSize, 1)));
        }

        public Task<ServiceResult<DirectoryPage<ClassListItem>>> ListClassesAsync(
            DirectoryScope scope, ClassListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<DirectoryPage<ClassListItem>>.Success(
                new DirectoryPage<ClassListItem>([Class(3)], 1, 20, 1)));

        public Task<ServiceResult<ClassDetailDto>> GetClassAsync(
            DirectoryScope scope, ulong classId, PageQuery rosterPage,
            CancellationToken cancellationToken) =>
            Task.FromResult(ClassDetail(classId, rosterPage));

        public Task<ServiceResult<SchoolDirectoryReferenceData>> GetReferenceDataAsync(
            DirectoryScope scope, ulong? academicYearId, CancellationToken cancellationToken) =>
            Task.FromResult(ServiceResult<SchoolDirectoryReferenceData>.Success(
                new SchoolDirectoryReferenceData([], [], [], [], [], [])));

        internal static ServiceResult<StudentDetailDto> Student(ulong studentId, string status) =>
            studentId == 404
                ? ServiceResult<StudentDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.StudentNotFound, "Không tìm thấy học sinh.")
                : ServiceResult<StudentDetailDto>.Success(new StudentDetailDto(
                    studentId, "HS7", "An", null, null, new DateOnly(2024, 9, 1), status, null, []));

        internal static ServiceResult<ClassDetailDto> ClassDetail(ulong classId, PageQuery page)
        {
            if (classId == 404)
            {
                return ServiceResult<ClassDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.ClassNotFound, "Không tìm thấy lớp học.");
            }

            var student = new ClassStudentItem(7, "HS7", "An", null, null, "6A", "ACTIVE");
            return ServiceResult<ClassDetailDto>.Success(new ClassDetailDto(
                Class(classId),
                new DirectoryPage<ClassStudentItem>([student], page.Page, page.PageSize, 1)));
        }

        internal static ClassListItem Class(ulong id) =>
            new(id, "6A", "6A", 1, "Khối 6", 1, "2025-2026", 1, "Cơ sở 1", null, null, 1, "ACTIVE");
    }

    private sealed class FakeSchoolDirectoryAdminService : ISchoolDirectoryAdminService
    {
        public Task<ServiceResult<StudentDetailDto>> CreateStudentAsync(
            ulong schoolId, CreateStudentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(FakeSchoolDirectoryService.Student(7, "ACTIVE"));

        public Task<ServiceResult<StudentDetailDto>> UpdateStudentAsync(
            ulong schoolId, ulong studentId, UpdateStudentRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(FakeSchoolDirectoryService.Student(studentId, "ACTIVE"));

        public Task<ServiceResult<StudentDetailDto>> TransferStudentClassAsync(
            ulong schoolId, ulong studentId, TransferStudentClassRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(FakeSchoolDirectoryService.Student(studentId, "ACTIVE"));

        public Task<ServiceResult<StudentDetailDto>> DeleteStudentAsync(
            ulong schoolId, ulong studentId, CancellationToken cancellationToken) =>
            Task.FromResult(FakeSchoolDirectoryService.Student(studentId, "INACTIVE"));

        public Task<ServiceResult<ClassDetailDto>> CreateClassAsync(
            ulong schoolId, CreateClassRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Code switch
            {
                "DUPLICATE" => ServiceResult<ClassDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.ClassCodeDuplicate, "Mã lớp đã tồn tại."),
                "INVALID" => ServiceResult<ClassDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.Validation, "Dữ liệu gửi lên không hợp lệ."),
                _ => FakeSchoolDirectoryService.ClassDetail(11, new PageQuery())
            });

        public Task<ServiceResult<ClassDetailDto>> UpdateClassAsync(
            ulong schoolId, ulong classId, UpdateClassRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(FakeSchoolDirectoryService.ClassDetail(classId, new PageQuery()));

        public Task<ServiceResult<ClassDetailDto>> DeleteClassAsync(
            ulong schoolId, ulong classId, CancellationToken cancellationToken) =>
            Task.FromResult(classId == 409
                ? ServiceResult<ClassDetailDto>.Failure(
                    SchoolDirectoryErrorCodes.ClassHasActiveStudents, "Lớp còn học sinh.")
                : FakeSchoolDirectoryService.ClassDetail(classId, new PageQuery()));
    }
}
