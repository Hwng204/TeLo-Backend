using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Services.Interface;
using Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WebAPI.Controllers;
using WebAPI.Security;

namespace WebAPI.IntegrationTests;

// Uses the real JWT authentication; only the password check and the matrix service are faked.
public sealed class LoginApiTests : IClassFixture<LoginApplicationFactory>
{
    private readonly LoginApplicationFactory factory;

    public LoginApiTests(LoginApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsAnAccessTokenThatOpensProtectedEndpoints()
    {
        using var client = factory.CreateClient();

        var login = await Login(client, "pht_a", "secret");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.True(body.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow);

        var token = body.GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
        Assert.Contains(claims, claim => claim.Type == "branch_id" && claim.Value == "1");
        Assert.Contains(claims, claim => claim.Value == "PHT");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/matrices/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrBlankCredentials_Returns401WithVietnameseMessage()
    {
        using var client = factory.CreateClient();

        using var wrong = await Login(client, "pht_a", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var text = await wrong.Content.ReadAsStringAsync();
        Assert.Contains("Tên đăng nhập hoặc mật khẩu không đúng", text);
        Assert.Contains("\"code\":\"Unauthorized\"", text);

        using var blank = await Login(client, "", "");
        Assert.Equal(HttpStatusCode.Unauthorized, blank.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutTokenOrWithATamperedToken_Returns401()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/matrices/1");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var login = await Login(client, "pht_a", "secret");
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token[..^3] + "abc");
        using var tampered = await client.GetAsync("/api/matrices/1");
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);
    }

    [Fact]
    public async Task UserWithAnyRoleCanLogin_AndTheTokenCarriesThatRole()
    {
        using var client = factory.CreateClient();

        using var login = await Login(client, "giao_vien", "secret");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
        Assert.Contains(claims, claim => claim.Value == "GIAO_VIEN");
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });
}

public sealed class LoginApplicationFactory : WebApplicationFactory<StudentsController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=localhost;Database=sep_contract;User=root;Password=test;");
        builder.UseSetting(
            "Jwt:SigningKey",
            "login-test-signing-key-with-at-least-32-chars");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=sep_contract;User=root;Password=test;"
            }));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserAuthenticationService>();
            services.AddScoped<IUserAuthenticationService, FakeUserAuthenticationService>();
            services.RemoveAll<IMatrixApplicationService>();
            services.AddScoped<IMatrixApplicationService, FakeMatrixApplicationService>();
        });
    }
}

public sealed class FakeUserAuthenticationService : IUserAuthenticationService
{
    public Task<AuthenticatedUser?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticatedUser? user = (request.Username, request.Password) switch
        {
            ("pht_a", "secret") => new AuthenticatedUser(2, "pht_a", ["PHT"], 1),
            ("giao_vien", "secret") => new AuthenticatedUser(30, "giao_vien", ["GIAO_VIEN"], 1),
            _ => null
        };

        return Task.FromResult(user);
    }
}

// The matrix feature only needs a matrix role; other roles log in but are refused by the matrix current user.
public sealed class MatrixCurrentUserTests
{
    [Fact]
    public void ReadsUserRoleAndBranchFromTheToken_AndRefusesRolesWithoutMatrixAccess()
    {
        var catalog = new ConfiguredMatrixRoleCatalog(new ConfigurationBuilder().Build());

        var pht = new HttpMatrixCurrentUser(Accessor("2", "PHT", "1"), catalog).Actor;
        Assert.Equal(2ul, pht.UserId);
        Assert.Equal(1ul, pht.BranchId);
        Assert.Equal(Domain.Entities.QuestionBank.MatrixActorRole.Pht, pht.Role);

        var principal = new HttpMatrixCurrentUser(Accessor("1", "HIEU_TRUONG", null), catalog).Actor;
        Assert.True(principal.IsPrincipal);

        var exception = Assert.Throws<Application.Common.MatrixApplicationException>(() =>
            _ = new HttpMatrixCurrentUser(Accessor("30", "GIAO_VIEN", "1"), catalog).Actor);
        Assert.Equal("Forbidden", exception.Code);
    }

    private static Microsoft.AspNetCore.Http.IHttpContextAccessor Accessor(
        string userId,
        string role,
        string? branchId)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
            new(System.Security.Claims.ClaimTypes.Role, role)
        };
        if (branchId is not null)
        {
            claims.Add(new System.Security.Claims.Claim("branch_id", branchId));
        }

        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(claims, "test"))
        };
        return new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context };
    }
}
