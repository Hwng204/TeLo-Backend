using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace WebAPI.Tests;

public sealed class TasksControllerTests
{
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/api/tasks" },
        { "GET", "/api/tasks/options" },
        { "GET", "/api/tasks/5" },
        { "DELETE", "/api/tasks/5" },
        { "POST", "/api/tasks/question" },
        { "PUT", "/api/tasks/question/5" },
        { "POST", "/api/tasks/exam" },
        { "PUT", "/api/tasks/exam/5" },
        { "PUT", "/api/tasks/matrix/5" }
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRouteRequiresAnAccount(string method, string url)
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(service.Method);
    }

    [Theory]
    [InlineData("STUDENT")]
    [InlineData("HOC_SINH")]
    [InlineData("ADMIN")]
    [InlineData("OperationalAdmin")]
    public async Task RolesOutsideTheSchoolTeamCannotOpenTasks(string role)
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, role);

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(service.Method);
    }

    [Theory]
    [InlineData("PHT", true, false, false)]
    [InlineData("TEAM_LEAD", false, true, false)]
    [InlineData("TO_TRUONG", false, true, false)]
    [InlineData("GIAO_VIEN", false, false, false)]
    [InlineData("TEACHER", false, false, false)]
    [InlineData("HIEU_TRUONG", false, false, true)]
    public async Task ActorComesFromTheTokenNotTheRequest(string role, bool pht, bool teamLead, bool principal)
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, role);

        var response = await client.GetAsync("/api/tasks?userId=99&page=2&pageSize=5&keyword=toán&taskType=QUESTION&status=OVERDUE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TaskActor(42, pht, teamLead, principal), service.Actor);
        Assert.Equal(new TaskListQuery(2, 5, "toán", "QUESTION", "OVERDUE"), service.Query);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRouteReachesItsServiceMethod(string method, string url)
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, "TEAM_LEAD");

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((method, url) switch
        {
            ("GET", "/api/tasks") => "list",
            ("GET", "/api/tasks/options") => "options",
            ("GET", _) => "get",
            ("DELETE", _) => "delete",
            ("POST", "/api/tasks/question") => "create-question",
            ("PUT", "/api/tasks/question/5") => "update-question",
            ("POST", "/api/tasks/exam") => "create-exam",
            ("PUT", "/api/tasks/exam/5") => "update-exam",
            _ => "update-matrix"
        }, service.Method);
        if (url.EndsWith("/5")) Assert.Equal(5ul, service.TaskId);
    }

    [Theory]
    [InlineData(TaskErrorCodes.Validation, 422)]
    [InlineData(TaskErrorCodes.BranchRequired, 403)]
    [InlineData(TaskErrorCodes.Forbidden, 403)]
    [InlineData(TaskErrorCodes.NotFound, 404)]
    [InlineData(TaskErrorCodes.Started, 409)]
    [InlineData(TaskErrorCodes.Completed, 409)]
    public async Task BusinessErrorsMapToHttpStatusWithTheirCode(string code, int status)
    {
        using var factory = Factory(new FakeTaskService { Error = code });
        using var client = Client(factory, "PHT");

        var response = await client.PutAsJsonAsync("/api/tasks/exam/5", new { name = "x" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(status, (int)response.StatusCode);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Lỗi thử", body.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task DeleteReportsTheNameOfTheRemovedTask()
    {
        using var factory = Factory(new FakeTaskService());
        using var client = Client(factory, "TEAM_LEAD");

        var body = await (await client.DeleteAsync("/api/tasks/5")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Đã xóa nhiệm vụ \"Biên soạn câu hỏi · Số và phép tính\".", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task QuestionTaskBodyIsBoundWithLessonCountsAndIgnoresOldReviewerFields()
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, "TEAM_LEAD");

        // Client cũ còn gửi reviewerUserId, reviewDueAt: bỏ qua, không lỗi.
        var response = await client.PostAsJsonAsync("/api/tasks/question", new
        {
            name = "Biên soạn", assignedToUserId = 7, dueAt = "2026-10-20T00:00:00", reviewerUserId = 8,
            reviewDueAt = "2026-10-25T00:00:00",
            lessons = new[] { new { lessonId = 3, knowledge = 8, understanding = 6, application = 4 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.IsType<SaveQuestionTaskRequest>(service.Body);
        Assert.Equal(7ul, request.AssignedToUserId);
        Assert.Equal(new QuestionTaskLessonRequest(3, 8, 6, 4), Assert.Single(request.Lessons!));
    }

    [Fact]
    public async Task ExamTaskBodyIsBoundWithPaperCountAndIgnoresOldExamFields()
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, "PHT");

        // Client cũ còn gửi examId, papers: bỏ qua; số đề lấy từ paperCount.
        var response = await client.PutAsJsonAsync("/api/tasks/exam/5", new
        {
            name = "Tạo đề", examMatrixId = 9, assignedToUserId = 7, dueAt = "2026-10-20T00:00:00", paperCount = 3,
            examId = 4, papers = new[] { "Đề chẵn" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SaveExamTaskRequest("Tạo đề", 9, 7, new DateTime(2026, 10, 20), 3), service.Body);
    }

    [Fact]
    public async Task NegativeQuestionCountIsRejectedBeforeTheService()
    {
        var service = new FakeTaskService();
        using var factory = Factory(service);
        using var client = Client(factory, "TEAM_LEAD");

        var response = await client.PostAsJsonAsync("/api/tasks/question", new
        {
            name = "Biên soạn", lessons = new[] { new { lessonId = 3, knowledge = -1, understanding = 0, application = 0 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.Method);
    }

    private static HttpRequestMessage Request(string method, string url) => new(new HttpMethod(method), url)
    {
        Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "Nhiệm vụ" }) : null
    };

    private static WebApplicationFactory<Program> Factory(FakeTaskService service) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
            builder.UseSetting("Jwt:SigningKey", "task-controller-test-key-at-least-32-characters");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = MatrixImportTestAuth.Scheme;
                    options.DefaultChallengeScheme = MatrixImportTestAuth.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, MatrixImportTestAuth>(MatrixImportTestAuth.Scheme, _ => { });
                services.RemoveAll<ITaskService>();
                services.AddSingleton<ITaskService>(service);
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.UserHeader, "42");
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.RoleHeader, role);
        return client;
    }

    private sealed class FakeTaskService : ITaskService
    {
        public string? Error { get; init; }
        public string? Method { get; private set; }
        public TaskActor? Actor { get; private set; }
        public TaskListQuery? Query { get; private set; }
        public ulong? TaskId { get; private set; }
        public object? Body { get; private set; }

        private static readonly TaskDetail Detail = new(5, TaskTypeCodes.Question, "Nhiệm vụ", null, MatrixTaskStatusCodes.Assigned,
            false, false, true, true, null, DateTime.UtcNow, null, null, null, null, null, null, []);

        public Task<ServiceResult<DirectoryPage<TaskListItem>>> ListAsync(TaskActor actor, TaskListQuery query, CancellationToken cancellationToken)
        {
            Query = query;
            return Answer("list", actor, null, null, new DirectoryPage<TaskListItem>([], query.Page, query.PageSize, 0));
        }

        public Task<ServiceResult<TaskDetail>> GetAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken) =>
            Answer("get", actor, taskId, null, Detail);

        public Task<ServiceResult<TaskOptions>> GetOptionsAsync(TaskActor actor, CancellationToken cancellationToken) =>
            Answer("options", actor, null, null, new TaskOptions([], [], []));

        public Task<ServiceResult<TaskDetail>> UpdateMatrixAsync(TaskActor actor, ulong taskId, CreateMatrixTaskRequest request, CancellationToken cancellationToken) =>
            Answer("update-matrix", actor, taskId, request, Detail);

        public Task<ServiceResult<TaskDetail>> CreateExamAsync(TaskActor actor, SaveExamTaskRequest request, CancellationToken cancellationToken) =>
            Answer("create-exam", actor, null, request, Detail);

        public Task<ServiceResult<TaskDetail>> UpdateExamAsync(TaskActor actor, ulong taskId, SaveExamTaskRequest request, CancellationToken cancellationToken) =>
            Answer("update-exam", actor, taskId, request, Detail);

        public Task<ServiceResult<TaskDetail>> CreateQuestionAsync(TaskActor actor, SaveQuestionTaskRequest request, CancellationToken cancellationToken) =>
            Answer("create-question", actor, null, request, Detail);

        public Task<ServiceResult<TaskDetail>> UpdateQuestionAsync(TaskActor actor, ulong taskId, SaveQuestionTaskRequest request, CancellationToken cancellationToken) =>
            Answer("update-question", actor, taskId, request, Detail);

        public Task<ServiceResult<string>> DeleteAsync(TaskActor actor, ulong taskId, CancellationToken cancellationToken) =>
            Answer("delete", actor, taskId, null, "Biên soạn câu hỏi · Số và phép tính");

        private Task<ServiceResult<T>> Answer<T>(string method, TaskActor actor, ulong? taskId, object? body, T value)
        {
            (Method, Actor, TaskId, Body) = (method, actor, taskId, body);
            return Task.FromResult(Error is null ? ServiceResult<T>.Success(value) : ServiceResult<T>.Failure(Error, "Lỗi thử"));
        }
    }
}
