using System.Net;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;

namespace WebAPI.Tests;

public sealed class MatrixImportTestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "MatrixImportTest";
    public const string UserHeader = "X-Matrix-Test-User";
    public const string RoleHeader = "X-Matrix-Test-Role";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || !Request.Headers.TryGetValue(RoleHeader, out var role))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, role.ToString())], Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}

public sealed class MatrixImportsControllerTests
{
    [Theory]
    [InlineData("GET", "/api/matrices/import/template.xlsx")]
    [InlineData("POST", "/api/matrices/import/preview")]
    [InlineData("POST", "/api/matrices/import")]
    public async Task Routes_RequireAuthentication(string method, string url)
    {
        using var factory = Factory(new FakeImportService());
        using var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/matrices/import/preview", "preview")]
    [InlineData("/api/matrices/import", "import")]
    public async Task Multipart_UsesScopedContextAndOriginalFile(string route, string method)
    {
        var service = new FakeImportService();
        using var factory = Factory(service);
        using var client = Client(factory);
        using var content = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "matrix.xlsx" } };
        var response = await client.PostAsync(route + "?academicContextId=10&semesterId=20&totalScore=15&name=Toan", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(method, service.Method);
        Assert.Equal(new MatrixImportContext(10, 20, 15, "Toan"), service.Context);
        Assert.Equal("matrix.xlsx", service.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, service.Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5 * 1024 * 1024 + 1)]
    public async Task EmptyOrOversizeFile_Returns422WithoutCallingService(int size)
    {
        var service = new FakeImportService();
        using var factory = Factory(service);
        using var client = Client(factory);
        using var content = new MultipartFormDataContent { { new ByteArrayContent(new byte[size]), "file", "matrix.xlsx" } };
        var response = await client.PostAsync("/api/matrices/import/preview?academicContextId=10", content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null(service.Method);
        Assert.Contains("ImportFileInvalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Template_ReturnsDownloadHeadersAndDefaults()
    {
        var service = new FakeImportService();
        using var factory = Factory(service);
        using var client = Client(factory);
        var response = await client.GetAsync("/api/matrices/import/template.xlsx?academicContextId=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("template.xlsx", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Equal(10, service.Context?.TotalScore);
    }

    [Theory]
    [InlineData("ImportFileInvalid", 422)]
    [InlineData("ImportHasInvalidRows", 422)]
    [InlineData("Forbidden", 403)]
    public async Task BusinessErrors_MapToHttpStatus(string code, int status)
    {
        using var factory = Factory(new FakeImportService { Error = code });
        using var client = Client(factory);
        using var content = new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "matrix.xlsx" } };
        var response = await client.PostAsync("/api/matrices/import?academicContextId=10", content);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("TEACHER")]
    [InlineData("STUDENT")]
    [InlineData("ADMIN")]
    public async Task RealImportService_RejectsRolesWithoutMatrixAccess(string role)
    {
        using var factory = Factory(new FakeImportService()).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMatrixImportService>();
            services.AddScoped<IMatrixImportService, Application.Services.Implement.MatrixImportService>();
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.UserHeader, "42");
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.RoleHeader, role);
        var response = await client.GetAsync("/api/matrices/import/template.xlsx?academicContextId=10");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Factory(FakeImportService service) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
            builder.UseSetting("Jwt:SigningKey", "matrix-import-test-key-at-least-32-characters");
            builder.ConfigureTestServices(services => {
                services.AddAuthentication(options => {
                    options.DefaultAuthenticateScheme = MatrixImportTestAuth.Scheme;
                    options.DefaultChallengeScheme = MatrixImportTestAuth.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, MatrixImportTestAuth>(MatrixImportTestAuth.Scheme, _ => { });
                services.RemoveAll<IMatrixImportService>();
                services.AddSingleton<IMatrixImportService>(service);
            });
        });
    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.UserHeader, "42");
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.RoleHeader, "PHT");
        return client;
    }
    private sealed class FakeImportService : IMatrixImportService
    {
        public MatrixImportContext? Context;
        public string? Method, Name, Error;
        public byte[]? Content;
        public Task<MatrixExportFile> BuildTemplateAsync(MatrixImportContext context, CancellationToken ct)
        { Context = context; return Task.FromResult(new MatrixExportFile("template.xlsx", [1, 2])); }
        public Task<MatrixImportPreview> PreviewAsync(MatrixImportContext context, string name, byte[] content, CancellationToken ct) => Answer("preview", context, name, content);
        public Task<MatrixImportPreview> ImportAsync(MatrixImportContext context, string name, byte[] content, CancellationToken ct) => Answer("import", context, name, content);
        private Task<MatrixImportPreview> Answer(string method, MatrixImportContext context, string name, byte[] content)
        {
            if (Error is not null) throw new MatrixApplicationException(Error, "Test error");
            (Method, Context, Name, Content) = (method, context, name, content);
            return Task.FromResult(new MatrixImportPreview(true, null, 10, 1, 1, [new(1, "NHAN_BIET", 1, 100)], []));
        }
    }
}
