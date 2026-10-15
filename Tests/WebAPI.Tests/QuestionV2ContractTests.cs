using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace WebAPI.Tests;

// Deliberately uses an unused local connection string. Authorization and model
// validation must finish before any persistence call; these tests never seed,
// migrate, open, or write a database.
public sealed class QuestionV2ContractTests
{
    [Theory]
    [InlineData("/api/question-banks/questions")]
    [InlineData("/api/tasks/5/question-package")]
    [InlineData("/api/tasks/5/papers/2/questions")]
    [InlineData("/api/exam-packages")]
    [InlineData("/api/exam-packages/2")]
    [InlineData("/api/roles/required")]
    public async Task RoutesStillRequireAuthentication(string path)
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("/api/question-banks/questions")]
    [InlineData("/api/tasks/5/question-package")]
    [InlineData("/api/exam-packages")]
    [InlineData("/api/roles/required")]
    public async Task StudentCannotAccessSchoolManagement(string path)
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Authenticate(client, "STUDENT");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("/api/tasks/5/question-package/submit")]
    [InlineData("/api/tasks/5/question-package/approve")]
    [InlineData("/api/tasks/5/question-package/reject")]
    [InlineData("/api/exam-packages/2/finalize")]
    [InlineData("/api/exam-packages/2/return-review")]
    [InlineData("/api/exam-packages/2/review-assignment")]
    [InlineData("/api/exam-packages/2/submit-review")]
    public async Task MissingVersionIsRejectedBeforeAnyDatabaseCall(string path)
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Authenticate(client, "TEACHER");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { })).StatusCode);
    }

    [Fact]
    public async Task RequiredRoleRestorationIsAnExplicitAdminOnlyAction()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/roles/required/restore", new { })).StatusCode);
        Authenticate(client, "TEACHER");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/roles/required/restore", new { })).StatusCode);
    }

    [Theory]
    [InlineData("TEAM_LEAD")]
    [InlineData("TO_TRUONG")]
    [InlineData("TEACHER")]
    [InlineData("HIEU_TRUONG")]
    public async Task ReviewerRolesCannotGenerateAnExamBeforePhtAssignsIt(string role)
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Authenticate(client, role);
        var response = await client.PostAsJsonAsync("/api/exam-packages", new { name = "Đề kiểm tra", matrixId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PhtMustGenerateAnExamInsteadOfAssigningBlankPapersToATeamLead()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        Authenticate(client, "PHT");
        var response = await client.PostAsJsonAsync("/api/tasks/exam", new
        {
            name = "Không giao soạn đề trống", examMatrixId = 1, assignedToUserId = 2,
            dueAt = "2030-01-01T00:00:00", paperCount = 2
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("EXAM_TASK_FLOW_CHANGED", body.GetProperty("error").GetProperty("code").GetString());
    }

    private static void Authenticate(HttpClient client, string role)
    {
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.UserHeader, "42");
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.RoleHeader, role);
    }

    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
        builder.UseSetting("Jwt:SigningKey", "question-v2-tests-key-at-least-thirty-two-characters");
        builder.ConfigureTestServices(services => services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = MatrixImportTestAuth.Scheme;
            options.DefaultChallengeScheme = MatrixImportTestAuth.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, MatrixImportTestAuth>(MatrixImportTestAuth.Scheme, _ => { }));
    });
}
