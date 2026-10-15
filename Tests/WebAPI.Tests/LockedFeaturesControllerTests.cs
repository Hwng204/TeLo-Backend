using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebAPI.Controllers;
using Xunit;

namespace WebAPI.Tests;

// Modules not yet moved to the ERD v2 tables answer 501 instead of 404/500 until their owners rewrite them.
public sealed class LockedFeaturesControllerTests
{
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/api/exams" },
        { "POST", "/api/exams" },
        { "GET", "/api/exams/5/rooms" },
        { "PUT", "/api/exams/5/subjects/2" },
        { "GET", "/api/schools/1/emails/templates" },
        { "GET", "/api/provinces" },
        { "POST", "/api/student-imports/preview" },
        { "DELETE", "/api/admin/schools/1/student-imports/3" }
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task LockedRoutesAnswer501WithTheLockCode(string method, string url)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.UserHeader, "42");
        client.DefaultRequestHeaders.Add(MatrixImportTestAuth.RoleHeader, "ADMIN");

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(LockedFeaturesController.ErrorCode, body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LockedRoutesStillRequireAnAccount()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/exams");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
            builder.UseSetting("Jwt:SigningKey", "locked-features-test-key-at-least-32-characters");
            builder.ConfigureTestServices(services => services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = MatrixImportTestAuth.Scheme;
                options.DefaultChallengeScheme = MatrixImportTestAuth.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, MatrixImportTestAuth>(MatrixImportTestAuth.Scheme, _ => { }));
        });
}
