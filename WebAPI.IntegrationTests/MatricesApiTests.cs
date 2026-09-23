using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.QuestionBank;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WebAPI.Controllers;

namespace WebAPI.IntegrationTests;

public sealed class MatricesApiTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public MatricesApiTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task MatrixListRequiresAuthentication()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/matrices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedMatrixListUsesApplicationService()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");

        using var response = await client.GetAsync("/api/matrices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("totalCount", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApplicationNotFoundBecomesProblemDetails()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");

        using var response = await client.GetAsync("/api/matrices/404");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("NotFound", body);
    }

    [Fact]
    public async Task CorsPreflightAllowsFrontendOriginAndVietnameseErrorsAreReturned()
    {
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/matrices")
        {
            Headers =
            {
                { "Origin", "http://localhost:5173" },
                { "Access-Control-Request-Method", "GET" },
                { "Access-Control-Request-Headers", "authorization" }
            }
        };
        using var preflightResponse = await client.SendAsync(preflight);
        Assert.Equal(
            "http://localhost:5173",
            preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");
        using var notFound = await client.GetAsync("/api/matrices/404");
        Assert.Contains("Không tìm thấy ma trận", await notFound.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MatrixExportReturnsExcelAttachment()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");

        using var response = await client.GetAsync("/api/matrices/1/export.xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task MatrixTaskCreateRouteReturnsCreated()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");

        using var response = await client.PostAsJsonAsync(
            "/api/matrix-tasks",
            new
            {
                assignedToUserId = 20,
                academicContextId = 100,
                semesterId = 2,
                name = "Integration task",
                description = "Integration task"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MatrixReferenceDataRouteRequiresAnAuthenticatedActor()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Role", "PHT");

        using var response = await client.GetAsync("/api/matrix-reference-data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class TestApplicationFactory : WebApplicationFactory<StudentsController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=localhost;Database=sep_contract;User=root;Password=test;");
        builder.UseSetting(
            "Jwt:SigningKey",
            "integration-test-signing-key-with-at-least-32-chars");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=sep_contract;User=root;Password=test;"
            }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
                options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.TestScheme,
                _ => { });

            services.RemoveAll<IMatrixApplicationService>();
            services.AddScoped<IMatrixApplicationService, FakeMatrixApplicationService>();
            services.RemoveAll<IMatrixTaskApplicationService>();
            services.AddScoped<IMatrixTaskApplicationService, FakeMatrixTaskApplicationService>();
        });
    }
}

public sealed class TestAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string TestScheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userId) ||
            !Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                userId.ToString()),
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role,
                role.ToString()),
            new System.Security.Claims.Claim("branch_id", "1")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, TestScheme);
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(
                new System.Security.Claims.ClaimsPrincipal(identity),
                TestScheme)));
    }
}

public sealed class FakeMatrixApplicationService : IMatrixApplicationService
{
    public Task<MatrixPage> ListAsync(MatrixListQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MatrixPage(
            Array.Empty<MatrixListItem>(),
            query.Page,
            query.PageSize,
            0));
    }

    public Task<MatrixResponse> GetAsync(ulong matrixId, CancellationToken cancellationToken)
    {
        if (matrixId == 404)
        {
            throw new MatrixApplicationException("NotFound", "Không tìm thấy ma trận.");
        }

        return Task.FromResult(new MatrixResponse(
            matrixId,
            "Integration matrix",
            MatrixStatusCodes.Approved,
            null,
            100,
            2,
            new[]
            {
                new MatrixDetailResponse(
                    1,
                    101,
                    "NHAN_BIET",
                    "MULTIPLE_CHOICE",
                    2,
                    100m,
                    1m)
            },
            2,
            1m,
            new[] { "View", "Export" }));
    }

    public Task<MatrixResponse> CreateAsync(SaveMatrixRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> UpdateAsync(ulong matrixId, SaveMatrixRequest request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task DeleteDraftAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> SubmitAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> RejectAsync(ulong matrixId, RejectMatrixRequest? request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> ApproveAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> ConfirmDirectAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> ArchiveAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixResponse> CloneAsync(ulong matrixId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixExportFile> ExportAsync(ulong matrixId, CancellationToken cancellationToken) =>
        Task.FromResult(new MatrixExportFile("Integration matrix.xlsx", new byte[] { 1, 2, 3 }));
}

public sealed class FakeMatrixTaskApplicationService : IMatrixTaskApplicationService
{
    public Task DeleteAsync(ulong taskId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<MatrixTaskResponse> CreateAsync(
        CreateMatrixTaskRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new MatrixTaskResponse(
            22,
            request.AssignedToUserId,
            request.AcademicContextId,
            request.SemesterId,
            request.DueAt,
            MatrixTaskStatusCodes.Assigned,
            "MATRIX",
            request.Description,
            null));
    }

    public Task<MatrixTaskPage> ListAsync(
        MatrixTaskQuery query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new MatrixTaskPage(
            Array.Empty<MatrixTaskListItem>(),
            query.Page,
            query.PageSize,
            0));
    }

    public Task<MatrixTaskPage> ListMineAsync(
        MatrixTaskQuery query,
        CancellationToken cancellationToken)
    {
        return ListAsync(query, cancellationToken);
    }

    public Task<MatrixReferenceData> GetReferenceDataAsync(
        ulong? academicContextId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new MatrixReferenceData(
            Array.Empty<Infrastructure.Models.MatrixAcademicContextOption>(),
            Array.Empty<Infrastructure.Models.MatrixSemesterOption>(),
            Array.Empty<Infrastructure.Models.MatrixLessonOption>(),
            Array.Empty<Infrastructure.Models.MatrixTeamLeadOption>()));
    }

    public Task<MatrixTaskResponse> GetAsync(ulong taskId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MatrixTaskResponse(
            taskId,
            20,
            100,
            2,
            null,
            MatrixTaskStatusCodes.Assigned,
            "MATRIX",
            null,
            null));
    }
}
