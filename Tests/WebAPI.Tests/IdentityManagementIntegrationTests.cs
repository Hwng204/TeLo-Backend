using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.Notification;
using Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    // Roles are global and have no version or "used" history any more (D3, R6); user edits keep security_version.
    [IdentityMySqlFact]
    public async Task RoleAndModuleWorkflow_EnforcesGlobalRolesHistoryAndSessionRevocation()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!);
        var database = "telo_identity_test_" + Guid.NewGuid().ToString("N");
        connection.Database = database;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync();

            var schoolA = new School { Code = "A", Name = "Trường A" };
            var schoolB = new School { Code = "B", Name = "Trường B" };
            var branchA = new SchoolBranch { Code = "A1", Name = "Phân hiệu A", School = schoolA };
            var branchA2 = new SchoolBranch { Code = "A2", Name = "Phân hiệu A2", School = schoolA };
            var branchB = new SchoolBranch { Code = "B1", Name = "Phân hiệu B", School = schoolB };
            var adminRole = new Role { Code = "ADMIN", Name = "Admin" };
            var admin = Account("admin", null);
            admin.UserRoles.Add(new UserRole { Role = adminRole });
            var userA = Account("userA", branchA);
            var userB = Account("userB", branchB);
            var userA2 = Account("userA2", branchA2);
            db.Users.AddRange(admin, userA, userA2, userB);
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
            var currentAdmin = await Read<UserDetailItem>(await client.GetAsync($"/api/users/{admin.Id}"));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/users/{admin.Id}/status",
                new IdentityStatusRequest("INACTIVE", currentAdmin.Version))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/users/{admin.Id}?version={currentAdmin.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync("/api/roles?pageSize=101")).StatusCode);
            var scopes = await Read<DirectoryPage<IdentityScopeItem>>(await client.GetAsync("/api/identity/scopes?kind=school&pageSize=1"));
            Assert.Equal(2, scopes.TotalCount);
            Assert.Single(scopes.Items);
            var branches = await Read<DirectoryPage<IdentityScopeItem>>(await client.GetAsync($"/api/identity/scopes?kind=branch&schoolId={schoolA.Id}"));
            Assert.Equal(2, branches.Items.Count);
            Assert.Contains(branches.Items, b => b.Id == branchA.Id);
            Assert.Contains(branches.Items, b => b.Id == branchA2.Id);

            // Full user lifecycle: normalize input, enforce uniqueness/versioning,
            // revoke sessions after credential/status changes, and retain audit history.
            var createdUser = await Read<UserDetailItem>(await client.PostAsJsonAsync("/api/users", new CreateUserRequest
            {
                Username = "managed.user",
                FullName = "  Người dùng quản lý  ",
                Password = "Initial-password-123!",
                MoetIdentifier = " MOET-001 ",
                SchoolBranchId = branchA.Id,
                Status = " active "
            }), HttpStatusCode.Created);
            // No user.email column: the account email comes from a teacher profile, "" without one.
            Assert.Equal("", createdUser.Email);
            Assert.Equal("Người dùng quản lý", createdUser.FullName);
            Assert.Equal("ACTIVE", createdUser.Status);
            Assert.Equal(branchA.Id, createdUser.SchoolBranchId);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/users", new CreateUserRequest
            {
                Username = createdUser.Username,
                FullName = "Trùng tên đăng nhập",
                Password = "Another-password-123!"
            })).StatusCode);

            var originalVersion = createdUser.Version;
            createdUser = await Read<UserDetailItem>(await client.PutAsJsonAsync($"/api/users/{createdUser.Id}", new UpdateUserRequest
            {
                Username = createdUser.Username,
                FullName = "Người dùng đã cập nhật",
                MoetIdentifier = createdUser.MoetIdentifier,
                SchoolBranchId = branchB.Id,
                NewPassword = "Updated-password-456!",
                Version = createdUser.Version
            }));
            Assert.Equal(branchB.Id, createdUser.SchoolBranchId);
            Assert.True(createdUser.Version > originalVersion);
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, createdUser.Username, "Initial-password-123!"));
            using (var managedSession = await Login(factory, createdUser.Username, "Updated-password-456!"))
                Assert.Equal(HttpStatusCode.Forbidden, (await managedSession.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{createdUser.Id}", new UpdateUserRequest
            {
                Username = createdUser.Username,
                FullName = createdUser.FullName,
                MoetIdentifier = createdUser.MoetIdentifier,
                SchoolBranchId = createdUser.SchoolBranchId,
                Version = originalVersion
            })).StatusCode);

            createdUser = await Read<UserDetailItem>(await client.PatchAsJsonAsync($"/api/users/{createdUser.Id}/password",
                new ResetUserPasswordRequest("Reset-password-789!", createdUser.Version)));
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, createdUser.Username, "Updated-password-456!"));
            using (var managedSession = await Login(factory, createdUser.Username, "Reset-password-789!"))
                Assert.Equal(HttpStatusCode.Forbidden, (await managedSession.GetAsync("/api/roles")).StatusCode);

            createdUser = await Read<UserDetailItem>(await client.PatchAsJsonAsync($"/api/users/{createdUser.Id}/status",
                new IdentityStatusRequest(" locked ", createdUser.Version)));
            Assert.Equal("LOCKED", createdUser.Status);
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, createdUser.Username, "Reset-password-789!"));
            createdUser = await Read<UserDetailItem>(await client.PatchAsJsonAsync($"/api/users/{createdUser.Id}/status",
                new IdentityStatusRequest("ACTIVE", createdUser.Version)));
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/users/{createdUser.Id}?version={createdUser.Version}")));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{createdUser.Id}")).StatusCode);
            // ERD v2 has no audit table, so user changes are no longer audited.

            // A configured principal alias is a system role and grants principal access.
            var principal = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles",
                new SaveRoleRequest { Code = "HeadTeacher", Name = "Hiệu trưởng" }), HttpStatusCode.Created);
            Assert.True(principal.IsSystem);
            principal = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{principal.Id}/users",
                new AssignRoleUsersRequest([userB.Id])));
            using (var principalSession = await Login(factory, "userB"))
                Assert.Equal(HttpStatusCode.OK, (await principalSession.GetAsync("/api/teachers/reference-data")).StatusCode);
            await Read<RoleItem>(await client.DeleteAsync($"/api/roles/{principal.Id}/users/{userB.Id}"));

            var legacyUser = Account("legacySpaces", null);
            legacyUser.UserRoles.Add(new UserRole { Role = new Role { Code = "LEGACY_SPACE ", Name = "Mã cũ có khoảng trắng" } });
            db.Users.Add(legacyUser);
            await db.SaveChangesAsync();
            using (var legacySession = await Login(factory, legacyUser.Username))
                Assert.Equal(HttpStatusCode.Forbidden, (await legacySession.GetAsync("/api/roles")).StatusCode);

            // A role applies to every school, including schools added after creating the role.
            var allSchools = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles",
                new SaveRoleRequest { Code = "ALL_SCHOOLS", Name = "Phụ trách chung" }), HttpStatusCode.Created);
            Assert.False(allSchools.IsSystem);
            var schoolC = new School { Code = "C", Name = "Trường C" };
            var branchC = new SchoolBranch { Code = "C1", Name = "Phân hiệu C", School = schoolC };
            var userC = Account("userC", branchC);
            db.Users.Add(userC);
            await db.SaveChangesAsync();
            var allEligible = await Read<DirectoryPage<IdentityUserItem>>(await client.GetAsync($"/api/users?eligibleForRoleId={allSchools.Id}"));
            Assert.Contains(allEligible.Items, u => u.Id == userA2.Id);
            Assert.Contains(allEligible.Items, u => u.Id == userB.Id);
            Assert.Contains(allEligible.Items, u => u.Id == userC.Id);
            allSchools = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{allSchools.Id}/users",
                new AssignRoleUsersRequest([userA2.Id, userB.Id, userC.Id])));
            Assert.Equal(3, allSchools.UserCount);
            foreach (var school in new[] { schoolA, schoolB, schoolC })
            {
                var schoolRoles = await Read<DirectoryPage<RoleItem>>(await client.GetAsync($"/api/roles?schoolId={school.Id}"));
                Assert.Contains(schoolRoles.Items, r => r.Id == allSchools.Id);
            }
            var globalBranchRoles = await Read<DirectoryPage<RoleItem>>(await client.GetAsync($"/api/roles?schoolId={schoolC.Id}&schoolBranchId={branchC.Id}"));
            Assert.Contains(globalBranchRoles.Items, r => r.Id == allSchools.Id);
            var request = new SaveRoleRequest { Code = "SCHOOL_REVIEW", Name = "Người duyệt" };
            var role = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", request), HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/roles", request)).StatusCode);
            var eligible = await Read<DirectoryPage<IdentityUserItem>>(await client.GetAsync($"/api/users?eligibleForRoleId={role.Id}"));
            Assert.Contains(eligible.Items, u => u.Id == userA.Id);
            Assert.Contains(eligible.Items, u => u.Id == userA2.Id);
            Assert.Contains(eligible.Items, u => u.Id == userB.Id);

            var wholeSchool = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles",
                new SaveRoleRequest { Code = "WHOLE_SCHOOL", Name = "Áp dụng toàn trường" }), HttpStatusCode.Created);
            wholeSchool = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{wholeSchool.Id}/users",
                new AssignRoleUsersRequest([userA2.Id])));
            Assert.Equal(1, wholeSchool.UserCount);
            Assert.NotNull(wholeSchool.DeleteBlockedReason);
            // Batch is atomic: one missing user means no grants at all.
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/roles/{role.Id}/users", new AssignRoleUsersRequest([userA.Id, 999_999]))).StatusCode);
            role = await Read<RoleItem>(await client.GetAsync($"/api/roles/{role.Id}"));
            Assert.Equal(0, role.UserCount);
            role = await Read<RoleItem>(await client.PostAsJsonAsync($"/api/roles/{role.Id}/users", new AssignRoleUsersRequest([userA.Id])));
            Assert.Equal(1, role.UserCount);
            Assert.False(role.CanDelete);
            Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/roles/{role.Id}")).StatusCode);
            request.Name = "Người duyệt cập nhật";
            role = await Read<RoleItem>(await client.PutAsJsonAsync($"/api/roles/{role.Id}", request));
            Assert.Equal("Người duyệt cập nhật", role.Name);
            using (var statusSession = await Login(factory, "userA"))
            {
                role = await Read<RoleItem>(await client.PatchAsJsonAsync($"/api/roles/{role.Id}/status", new IdentityStatusRequest("INACTIVE", 0)));
                Assert.Equal(HttpStatusCode.Unauthorized, (await statusSession.GetAsync("/api/roles")).StatusCode);
            }
            using (var relogin = await Login(factory, "userA"))
            {
                var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(relogin.DefaultRequestHeaders.Authorization!.Parameter);
                Assert.DoesNotContain(jwt.Claims, c => c.Value == "SCHOOL_REVIEW");
            }
            role = await Read<RoleItem>(await client.DeleteAsync($"/api/roles/{role.Id}/users/{userA.Id}"));
            Assert.Equal(0, role.UserCount);
            // No "used" history any more: a role without users, permissions or notification links can be deleted.
            Assert.True(role.CanDelete);
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/roles/{role.Id}")));

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
            Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/roles/{adminRole.Id}/status", new IdentityStatusRequest("INACTIVE", 0))).StatusCode);

            var unused = await Read<RoleItem>(await client.PostAsJsonAsync("/api/roles", new SaveRoleRequest { Code = "UNUSED_ROLE", Name = "Chưa sử dụng" }), HttpStatusCode.Created);
            unused = await Read<RoleItem>(await client.GetAsync($"/api/roles/{unused.Id}"));
            Assert.True(unused.CanDelete);
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/roles/{unused.Id}")));
            var page = await Read<DirectoryPage<RoleItem>>(await client.GetAsync("/api/roles?page=1&pageSize=1&search=ROLE"));
            Assert.Single(page.Items);
            Assert.Equal(2, page.TotalCount);

            var module = await Read<ModuleItem>(await client.PostAsJsonAsync("/api/modules", new SaveModuleRequest { Code = "TEST_MODULE", Name = "Module mới" }), HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/modules", new SaveModuleRequest { Code = "OTHER_CODE", Name = "Module mới" })).StatusCode);
            module = await Read<ModuleItem>(await client.PutAsJsonAsync($"/api/modules/{module.Id}", new SaveModuleRequest { Code = module.Code, Name = "Module đã sửa" }));
            db.Navbars.Add(new Navbar { ModuleId = module.Id, Name = "Mục điều hướng" });
            await db.SaveChangesAsync();
            module = await Read<ModuleItem>(await client.PatchAsJsonAsync($"/api/modules/{module.Id}/status", new IdentityStatusRequest("INACTIVE", 0)));
            Assert.False(module.CanDelete);
            Assert.Equal(1, module.NavbarCount);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/modules/{module.Id}")).StatusCode);
            var modulePage = await Read<DirectoryPage<ModuleItem>>(await client.GetAsync("/api/modules?status=INACTIVE&pageSize=1"));
            Assert.Equal(module.Id, Assert.Single(modulePage.Items).Id);
            var spare = await Read<ModuleItem>(await client.PostAsJsonAsync("/api/modules", new SaveModuleRequest { Code = "SPARE_MODULE", Name = "Module dự phòng" }), HttpStatusCode.Created);
            Assert.True(await Read<bool>(await client.DeleteAsync($"/api/modules/{spare.Id}")));

            // Concurrent mutual revocation must leave an administrator. The actor
            // is re-authorized under a transaction lock, not only at JWT validation.
            var otherAdminRole = new Role { Code = "OperationalAdmin", Name = "Admin vận hành" };
            db.UserRoles.Add(new UserRole { UserId = userB.Id, Role = otherAdminRole });
            await db.SaveChangesAsync();
            using var otherAdmin = await Login(factory, "userB");
            var revocations = await Task.WhenAll(
                client.DeleteAsync($"/api/roles/{otherAdminRole.Id}/users/{userB.Id}"),
                otherAdmin.DeleteAsync($"/api/roles/{adminRole.Id}/users/{admin.Id}"));
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

    // Roles are global, so a disabled school must revoke non-teacher staff through user.branch_id itself.
    [IdentityMySqlFact]
    public async Task DisablingSchool_RevokesStaffAndTeacherSessions_ButNotBranchlessAdmin()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!);
        var database = "telo_identity_test_" + Guid.NewGuid().ToString("N");
        connection.Database = database;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var school = new School { Code = "S", Name = "Trường S" };
            var branch = new SchoolBranch { Code = "S1", Name = "Phân hiệu S", School = school };
            var admin = Account("admin", null);
            var staff = Account("staff", branch);
            var teacher = Account("teacher", branch);
            teacher.Teachers.Add(new Teacher());
            var branchlessTeacher = Account("branchlessTeacher", null);
            branchlessTeacher.Teachers.Add(new Teacher());
            db.Users.AddRange(admin, staff, teacher, branchlessTeacher);
            await db.SaveChangesAsync();

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "identity-integration-tests-signing-key-32-characters");
            });
            // A teacher must still have an employing branch to log in.
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, "branchlessTeacher", "Identity-test-password-123"));
            using var adminSession = await Login(factory, "admin");
            using var staffSession = await Login(factory, "staff");
            using var teacherSession = await Login(factory, "teacher");
            Assert.Equal(HttpStatusCode.Forbidden, (await staffSession.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await teacherSession.GetAsync("/api/roles")).StatusCode);

            // Disabling the school does not bump security_version; the active-unit check alone must revoke.
            await db.Schools.Where(s => s.Id == school.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "INACTIVE"));
            Assert.Equal(HttpStatusCode.Unauthorized, (await staffSession.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await teacherSession.GetAsync("/api/roles")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, "staff", "Identity-test-password-123"));
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatus(factory, "teacher", "Identity-test-password-123"));
            Assert.NotEqual(HttpStatusCode.Unauthorized, (await adminSession.GetAsync("/api/roles")).StatusCode);
            using (await Login(factory, "admin")) { }
        }
        finally
        {
            Assert.StartsWith("telo_identity_test_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static User Account(string username, SchoolBranch? branch)
    {
        var user = new User { Username = username, FullName = username, SchoolBranch = branch };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Identity-test-password-123");
        return user;
    }
    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory, string username,
        string password = "Identity-test-password-123")
    {
        var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<HttpStatusCode> LoginStatus(WebApplicationFactory<Program> factory, string username, string password)
    {
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        return response.StatusCode;
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
