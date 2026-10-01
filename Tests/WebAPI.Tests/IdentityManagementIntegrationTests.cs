using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

// Opt-in real-MySQL tests. Always creates its own randomly named database; never
// migrates or writes the database named in TELO_TEST_MYSQL. No production credentials in source.
public sealed class IdentityMySqlFactAttribute : FactAttribute
{
    public IdentityMySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")))
            Skip = "Set TELO_TEST_MYSQL to run isolated MySQL integration tests.";
    }
}

public sealed class IdentityManagementIntegrationTests
{
    [Fact]
    public async Task ProductionStartupDoesNotCreateDemoAccountsOrSchema()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Port=9;Database=unused;User=unused;Password=unused;Connection Timeout=1;");
            builder.UseSetting("Jwt:SigningKey", "identity-integration-tests-signing-key-32-characters");
        });
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/roles")).StatusCode);
    }

    [IdentityMySqlFact]
    public async Task RoleAndModuleWorkflow_EnforcesScopesHistoryConcurrencyAndSessionRevocation()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!);
        var database = "telo_identity_test_" + Guid.NewGuid().ToString("N");
        connection.Database = database;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            // Exercise upgrading populated legacy modules, not just an empty schema.
            await db.GetService<IMigrator>().MigrateAsync("20260926120000_AcademicYearSystemScope");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO modules(name,status) VALUES ('Legacy One','ACTIVE'),('Legacy Two','ACTIVE')");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO roles(code,name) VALUES ('LEGACY_CATALOG','Vai trò có sẵn');
                INSERT INTO users(username,email,password_hash,full_name,status) VALUES ('legacy','legacy@test.invalid','unused-test-hash','Legacy','ACTIVE');
                INSERT INTO user_roles(user_id,role_id) SELECT u.id,r.id FROM users u CROSS JOIN roles r WHERE u.username='legacy' AND r.code='LEGACY_CATALOG';
                """);
            await db.Database.MigrateAsync();
            var legacy = await db.Modules.OrderBy(m => m.Id).ToArrayAsync();
            Assert.Equal(2, legacy.Length);
            Assert.All(legacy, m => Assert.Equal($"MODULE_{m.Id}", m.Code));
            Assert.True(await db.UserRoles.AnyAsync(ur => ur.User.Username == "legacy" && ur.Role.UsedAt != null));
            Assert.True(await db.IdentityAudits.AnyAsync(a => a.Action == "BASELINE_GRANT"));

            var schoolA = new School { Code = "A", Name = "Trường A" };
            var schoolB = new School { Code = "B", Name = "Trường B" };
            var branchA = new SchoolBranch { Code = "A1", Name = "Phân hiệu A", School = schoolA };
            var branchB = new SchoolBranch { Code = "B1", Name = "Phân hiệu B", School = schoolB };
            var adminRole = new Role { Code = "ADMIN", Name = "Admin", IsSystem = true };
            var admin = Account("admin", null);
            admin.UserRoles.Add(new UserRole { Role = adminRole });
            var userA = Account("userA", branchA);
            var userB = Account("userB", branchB);
            db.Users.AddRange(admin, userA, userB);
            await db.SaveChangesAsync();

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("MatrixAuth:PrincipalRoleCodes:2", "HeadTeacher");
                builder.UseSetting("SchoolDirectoryAuth:AdminRoleCodes:1", "DirectoryAdmin");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "identity-integration-tests-signing-key-32-characters");
            });
            using var anonymous = factory.CreateClient();
            anonymous.BaseAddress = new Uri("https://localhost");
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/roles")).StatusCode);
            using var client = await Login(factory, "admin");
            using var employee = await Login(factory, "userA");
            Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync("/api/roles?pageSize=101")).StatusCode);
            var scopes = await Read<DirectoryPage<IdentityScopeItem>>(await client.GetAsync("/api/identity/scopes?kind=school&pageSize=1"));
            Assert.Equal(2, scopes.TotalCount);
            Assert.Single(scopes.Items);
            var branches = await Read<DirectoryPage<IdentityScopeItem>>(await client.GetAsync($"/api/identity/scopes?kind=branch&schoolId={schoolA.Id}"));
            Assert.Equal(branchA.Id, Assert.Single(branches.Items).Id);

            // These aliases are interpreted globally by existing business APIs.
            foreach (var code in new[] { "PRINCIPAL", "HIEU_TRUONG", "HeadTeacher", "DirectoryAdmin" })
            {
                Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/roles",
                    new SaveRoleRequest { Code = code, Name = code, SchoolId = schoolA.Id, SchoolBranchId = branchA.Id })).StatusCode);
            }
            var principal = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles",
                new SaveRoleRequest { Code = "HeadTeacher", Name = "Hiệu trưởng" }), HttpStatusCode.Created);
            Assert.True(principal.IsSystem);
            principal = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{principal.Id}/users",
                new AssignRoleUsersRequest([userB.Id], principal.Version)));
            using (var principalSession = await Login(factory, "userB"))
                Assert.Equal(HttpStatusCode.OK, (await principalSession.GetAsync("/api/teachers/reference-data")).StatusCode);
            await Read<RoleItem>(await client.DeleteAsync($"/api/roles/{principal.Id}/users/{userB.Id}?version={principal.Version}"));

            var legacyUser = Account("legacySpaces", null);
            legacyUser.UserRoles.Add(new UserRole { Role = new Role { Code = "LEGACY_SPACE ", Name = "Mã cũ có khoảng trắng" } });
            db.Users.Add(legacyUser);
            await db.SaveChangesAsync();
            using (var legacySession = await Login(factory, legacyUser.Username))
                Assert.Equal(HttpStatusCode.Forbidden, (await legacySession.GetAsync("/api/roles")).StatusCode);

            var request = new SaveRoleRequest { Code = "SCHOOL_REVIEW", Name = "Người duyệt", SchoolId = schoolA.Id, SchoolBranchId = branchA.Id };
            var role = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", request), HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/roles", request)).StatusCode);
            var eligible = await Read<DirectoryPage<IdentityUserItem>>(await client.GetAsync($"/api/users?eligibleForRoleId={role.Id}"));
            Assert.Equal(userA.Id, Assert.Single(eligible.Items).Id);
            // Batch is atomic: one mismatched user means no grants at all.
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/roles/{role.Id}/users", new AssignRoleUsersRequest([userA.Id, userB.Id], role.Version))).StatusCode);
            role = await Read<RoleItem>(await client.GetAsync($"/api/roles/{role.Id}"));
            Assert.Equal(0, role.UserCount);
            role = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{role.Id}/users", new AssignRoleUsersRequest([userA.Id], role.Version)));
            Assert.Equal(1, role.UserCount);
            Assert.False(role.CanDelete);
            Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/roles")).StatusCode);
            using (var scopedSession = await Login(factory, "userA"))
            {
                schoolA.Status = "INACTIVE";
                await db.SaveChangesAsync();
                Assert.Equal(HttpStatusCode.Unauthorized, (await scopedSession.GetAsync("/api/roles")).StatusCode);
                request.Version = role.Version;
                request.Description = "Vẫn được sửa mô tả khi trường ngừng hoạt động";
                role = await Read<RoleItem>(await client.PutAsJsonAsync($"/api/roles/{role.Id}", request));
                schoolA.Status = "ACTIVE";
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/roles/{role.Id}?version={role.Version}")).StatusCode);
            request.Version = 1;
            var stale = await client.PutAsJsonAsync($"/api/roles/{role.Id}", request);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("STALE_VERSION", (await stale.Content.ReadFromJsonAsync<ApiResponse<RoleItem>>())!.Error!.Code);
            request.Version = role.Version;
            request.SchoolId = schoolB.Id;
            request.SchoolBranchId = branchB.Id;
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/roles/{role.Id}", request)).StatusCode);
            request.SchoolId = schoolA.Id;
            request.SchoolBranchId = branchA.Id;
            request.Name = "Người duyệt cập nhật";
            role = await Read<RoleItem>(await client.PutAsJsonAsync($"/api/roles/{role.Id}", request));
            using (var scopeSession = await Login(factory, "userA"))
            {
                request.Version = role.Version;
                request.SchoolBranchId = null;
                role = await Read<RoleItem>(await client.PutAsJsonAsync($"/api/roles/{role.Id}", request));
                Assert.Equal(1, role.UserCount);
                Assert.Null(role.SchoolBranchId);
                Assert.Equal(HttpStatusCode.Unauthorized, (await scopeSession.GetAsync("/api/roles")).StatusCode);
            }
            using (var statusSession = await Login(factory, "userA"))
            {
                role = await Read<RoleItem>(await client.PatchAsJsonAsync($"/api/roles/{role.Id}/status", new IdentityStatusRequest("INACTIVE", role.Version)));
                Assert.Equal(HttpStatusCode.Unauthorized, (await statusSession.GetAsync("/api/roles")).StatusCode);
            }
            using (var relogin = await Login(factory, "userA"))
            {
                var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(relogin.DefaultRequestHeaders.Authorization!.Parameter);
                Assert.DoesNotContain(jwt.Claims, c => c.Value == "SCHOOL_REVIEW");
            }
            role = await Read<RoleItem>(await client.DeleteAsync($"/api/roles/{role.Id}/users/{userA.Id}?version={role.Version}"));
            Assert.Equal(0, role.UserCount);
            Assert.False(role.CanDelete);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/roles/{role.Id}?version={role.Version}")).StatusCode);
            var audit = await db.IdentityAudits.AsNoTracking().Where(a => a.EntityType == "ROLE" && a.EntityId == role.Id).Select(a => a.Action).ToArrayAsync();
            Assert.Contains("GRANT", audit);
            Assert.Contains("REVOKE", audit);
            Assert.Contains("UPDATE", audit);

            // Multi-role replacement and role/member mutations use the same grant model.
            var second = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", new SaveRoleRequest { Code = "SECOND_ROLE", Name = "Vai trò hai" }), HttpStatusCode.Created);
            var third = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", new SaveRoleRequest { Code = "THIRD_ROLE", Name = "Vai trò ba" }), HttpStatusCode.Created);
            var userRoles = await Read<UserRolesDetail>(await client.GetAsync($"/api/users/{userA.Id}/roles"));
            var userVersion = userRoles.User.Version;
            userRoles = await Read<UserRolesDetail>(await client.PutAsJsonAsync($"/api/users/{userA.Id}/roles", new AssignUserRolesRequest([second.Id, third.Id], userVersion)));
            Assert.Equal(2, userRoles.Roles.Count);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{userA.Id}/roles", new AssignUserRolesRequest([], userVersion))).StatusCode);
            var adminDetail = await Read<UserRolesDetail>(await client.GetAsync($"/api/users/{admin.Id}/roles"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{admin.Id}/roles", new AssignUserRolesRequest([], adminDetail.User.Version))).StatusCode);
            var adminItem = await Read<RoleItem>(await client.GetAsync($"/api/roles/{adminRole.Id}"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/roles/{adminRole.Id}/status", new IdentityStatusRequest("INACTIVE", adminItem.Version))).StatusCode);

            var unused = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", new SaveRoleRequest { Code = "UNUSED_ROLE", Name = "Chưa sử dụng" }), HttpStatusCode.Created);
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/roles/{unused.Id}?version={unused.Version}")));
            var page = await Read<DirectoryPage<RoleItem>>(await client.GetAsync("/api/roles?page=1&pageSize=1&search=ROLE"));
            Assert.Single(page.Items);
            Assert.Equal(2, page.TotalCount);

            var module = await Read<ModuleItem>(await client.PostAsJsonAsync("/api/modules", new SaveModuleRequest { Code = "TEST_MODULE", Name = "Module mới" }), HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/modules", new SaveModuleRequest { Code = "OTHER_CODE", Name = "Module mới" })).StatusCode);
            module = await Read<ModuleItem>(await client.PutAsJsonAsync($"/api/modules/{module.Id}", new SaveModuleRequest { Code = module.Code, Name = "Module đã sửa", Version = module.Version }));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/modules/{module.Id}/status", new IdentityStatusRequest("INACTIVE", 1))).StatusCode);
            db.Navbars.Add(new Navbar { ModuleId = module.Id, Name = "Mục điều hướng" });
            await db.SaveChangesAsync();
            module = await Read<ModuleItem>(await client.PatchAsJsonAsync($"/api/modules/{module.Id}/status", new IdentityStatusRequest("INACTIVE", module.Version)));
            Assert.False(module.CanDelete);
            Assert.Equal(1, module.NavbarCount);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/modules/{module.Id}?version={module.Version}")).StatusCode);
            var modulePage = await Read<DirectoryPage<ModuleItem>>(await client.GetAsync("/api/modules?status=INACTIVE&pageSize=1"));
            Assert.Equal(module.Id, Assert.Single(modulePage.Items).Id);
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/modules/{legacy[0].Id}?version=1")));

            // Concurrent mutual revocation must leave an administrator. The actor
            // is re-authorized under a transaction lock, not only at JWT validation.
            var otherAdminRole = new Role { Code = "OperationalAdmin", Name = "Admin vận hành", IsSystem = true };
            db.UserRoles.Add(new UserRole { UserId = userB.Id, Role = otherAdminRole });
            await db.SaveChangesAsync();
            using var otherAdmin = await Login(factory, "userB");
            var revocations = await Task.WhenAll(
                client.DeleteAsync($"/api/roles/{otherAdminRole.Id}/users/{userB.Id}?version=1"),
                otherAdmin.DeleteAsync($"/api/roles/{adminRole.Id}/users/{admin.Id}?version={adminItem.Version}"));
            Assert.True(revocations.Count(r => r.IsSuccessStatusCode) <= 1);
            Assert.True(await db.UserRoles.AsNoTracking().AnyAsync(ur => ur.User.Status == "ACTIVE" && ur.Role.Status == "ACTIVE" &&
                (ur.Role.Code == "ADMIN" || ur.Role.Code == "OperationalAdmin")));
        }
        finally
        {
            Assert.StartsWith("telo_identity_test_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static User Account(string username, SchoolBranch? branch)
    {
        var user = new User { Username = username, Email = username + "@test.invalid", FullName = username, SchoolBranch = branch };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Identity-test-password-123");
        return user;
    }
    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Identity-test-password-123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {text}");
        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(envelope!.Success);
        return envelope.Data!;
    }
}
