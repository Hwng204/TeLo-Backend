using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

public sealed class DirectoryAcademicYearIntegrationTests
{
    [IdentityMySqlFact]
    public async Task SelectedYearScopesStudentsClassesAndTeachersWithoutChangingActiveYear()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!);
        var database = "telo_directory_year_test_" + Guid.NewGuid().ToString("N");
        connection.Database = database;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var old = Year(2025, "CLOSED");
            var current = Year(2026, "ACTIVE");
            var school = new School { Code = "A", Name = "School A" };
            var otherSchool = new School { Code = "B", Name = "School B" };
            var branch = new SchoolBranch { Code = "A1", Name = "Branch A", School = school };
            var otherBranch = new SchoolBranch { Code = "B1", Name = "Branch B", School = otherSchool };
            var grade = new GradeLevel { Name = "Grade 10" };
            var oldClass = Class("Old class", old, branch, grade);
            var finalClass = Class("Final old class", old, branch, grade);
            var currentClass = Class("Current class", current, branch, grade);
            var foreignClass = Class("Foreign class", old, otherBranch, grade);
            var shared = new Student { Code = "SHARED", FullName = "A Shared" };
            Enroll(shared, oldClass, "TRANSFERRED_OUT", new(2025, 8, 15));
            Enroll(shared, finalClass, "COMPLETED", new(2026, 1, 1));
            Enroll(shared, currentClass, "ACTIVE", new(2026, 8, 15));
            var oldOnly = new Student { Code = "OLD", FullName = "B Old" };
            Enroll(oldOnly, finalClass, "COMPLETED", new(2025, 8, 15));
            var currentOnly = new Student { Code = "CURRENT", FullName = "C Current" };
            Enroll(currentOnly, currentClass, "ACTIVE", new(2026, 8, 15));
            var foreign = new Student { Code = "FOREIGN", FullName = "Foreign" };
            Enroll(foreign, foreignClass, "COMPLETED", new(2025, 8, 15));
            var admin = new User { Username = "year_admin", Email = "admin@test.invalid", FullName = "Admin" };
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, "Year-test-password-123");
            admin.UserRoles.Add(new UserRole { Role = new Role { Code = "OperationalAdmin", Name = "Admin", IsSystem = true } });
            db.Users.Add(admin);
            var earlierTeacher = Teacher("earlier", branch, new(2025, 8, 15));
            var laterTeacher = Teacher("later", branch, new(2026, 8, 15));
            var unknownDateTeacher = Teacher("unknown", branch, null);
            var boundaryTeacher = Teacher("boundary", branch, old.EndDate);
            var outsideTeacher = Teacher("outside", otherBranch, new(2025, 8, 15));
            db.Teachers.AddRange(earlierTeacher, laterTeacher, unknownDateTeacher, boundaryTeacher, outsideTeacher);
            db.Students.AddRange(shared, oldOnly, currentOnly, foreign);
            await db.SaveChangesAsync();

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "directory-year-integration-test-signing-key-32-characters");
            });
            using var client = factory.CreateClient();
            client.BaseAddress = new Uri("https://localhost");
            var login = await client.PostAsJsonAsync("/api/auth/login", new { username = admin.Username, password = "Year-test-password-123" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var token = await login.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
            var path = $"/api/admin/schools/{school.Id}/students";
            var historical = await Read<DirectoryPage<StudentListItem>>(client, $"{path}?academicYearId={old.Id}&pageSize=1");
            Assert.Equal(2, historical.TotalCount);
            var row = Assert.Single(historical.Items);
            Assert.Equal(shared.Id, row.Id);
            Assert.Equal(finalClass.Id, row.ClassId);
            var present = await Read<DirectoryPage<StudentListItem>>(client, $"{path}?academicYearId={current.Id}");
            Assert.Equal(2, present.TotalCount);
            Assert.All(present.Items, item => Assert.Equal(currentClass.Id, item.ClassId));
            var formerClass = await Read<DirectoryPage<StudentListItem>>(client, $"{path}?academicYearId={old.Id}&classId={oldClass.Id}");
            Assert.Empty(formerClass.Items);
            var other = await Read<DirectoryPage<StudentListItem>>(client, $"{path}?academicYearId={old.Id}&schoolBranchId={otherBranch.Id}");
            Assert.Empty(other.Items);
            var legacy = await Read<DirectoryPage<StudentListItem>>(client, path);
            Assert.Equal(3, legacy.TotalCount);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{path}?academicYearId=0")).StatusCode);
            var classes = await Read<DirectoryPage<ClassListItem>>(client, $"/api/admin/schools/{school.Id}/classes?academicYearId={old.Id}");
            Assert.Equal(2, classes.TotalCount);
            Assert.All(classes.Items, item => Assert.Equal(old.Id, item.AcademicYearId));
            var teachersPath = $"/api/admin/schools/{school.Id}/branches/{branch.Id}/teachers";
            var earlierTeachers = await Read<TeacherPage<TeacherListItem>>(client, $"{teachersPath}?academicYearId={old.Id}&pageSize=1");
            Assert.Equal(3, earlierTeachers.TotalCount);
            Assert.Single(earlierTeachers.Items);
            var currentTeachers = await Read<TeacherPage<TeacherListItem>>(client, $"{teachersPath}?academicYearId={current.Id}");
            Assert.Equal(4, currentTeachers.TotalCount);
            Assert.Contains(currentTeachers.Items, item => item.Id == laterTeacher.Id);
            Assert.DoesNotContain(currentTeachers.Items, item => item.Id == outsideTeacher.Id);
            Assert.Equal(4, (await Read<TeacherPage<TeacherListItem>>(client, teachersPath)).TotalCount);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{teachersPath}?academicYearId=0")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{teachersPath}?academicYearId=999999")).StatusCode);
            Assert.Equal(current.Id, await db.AcademicYears.Where(year => year.Status == "ACTIVE").Select(year => year.Id).SingleAsync());
        }
        finally
        {
            Assert.StartsWith("telo_directory_year_test_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static AcademicYear Year(int start, string status)
    {
        var year = new AcademicYear { Name = $"{start}-{start + 1}", Status = status, StartDate = new(start, 8, 15), EndDate = new(start + 1, 5, 31) };
        year.AssignCode($"AY_{start}");
        return year;
    }

    private static SchoolClass Class(string name, AcademicYear year, SchoolBranch branch, GradeLevel grade) =>
        new() { Code = name, Name = name, AcademicYear = year, SchoolBranch = branch, GradeLevel = grade };

    private static void Enroll(Student student, SchoolClass schoolClass, string status, DateOnly start) =>
        student.Enrollments.Add(new StudentEnrollment { SchoolClass = schoolClass, AcademicYear = schoolClass.AcademicYear, Status = status, StartedOn = start });

    private static Teacher Teacher(string name, SchoolBranch branch, DateOnly? joinedOn) => new()
    {
        StaffCode = name.ToUpperInvariant(), JoinedOn = joinedOn,
        User = new User { Username = name, FullName = name, Email = name + "@test.invalid", PasswordHash = "unused-test-hash", SchoolBranch = branch },
    };

    private static async Task<T> Read<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<ApiResponse<T>>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Data!;
    }
}
