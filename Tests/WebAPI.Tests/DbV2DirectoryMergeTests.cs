using Application.Services.Interface;
using Application.Services.Implement;
using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

public sealed class DbV2DirectoryMergeTests
{
    [Fact]
    public void SplitDirectoryServicesResolveWithoutLegacyRegistrations()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User=unused;Password=unused;");
            builder.UseSetting("Jwt:SigningKey", "db-v2-merge-test-signing-key-at-least-32-characters");
        });
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IClassService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStudentService>());
        Assert.IsType<ClassRepository>(scope.ServiceProvider.GetRequiredService<IClassRepository>());
        Assert.IsType<StudentRepository>(scope.ServiceProvider.GetRequiredService<IStudentRepository>());
        Assert.IsType<SchoolRepository>(scope.ServiceProvider.GetRequiredService<ISchoolRepository>());
    }

    [IdentityMySqlFact]
    public async Task SplitRepositoriesKeepStudentUsersScopesAndTransferHistory()
    {
        var database = "telo_dbv2_merge_" + Guid.NewGuid().ToString("N");
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!) { Database = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options);
        try
        {
            await db.Database.MigrateAsync();
            var school = new School { Code = "MERGE", Name = "Merge school" };
            var otherSchool = new School { Code = "OTHER", Name = "Other school" };
            var branch = new SchoolBranch { School = school, Code = "A", Name = "Branch A" };
            var otherBranch = new SchoolBranch { School = otherSchool, Code = "B", Name = "Branch B" };
            var year = new AcademicYear { Name = "2026-2027", Status = "ACTIVE" };
            var grade = new GradeLevel { Name = "Grade 5" };
            var classA = new SchoolClass { SchoolBranch = branch, AcademicYear = year, GradeLevel = grade, Name = "5A" };
            var classB = new SchoolClass { SchoolBranch = branch, AcademicYear = year, GradeLevel = grade, Name = "5B" };
            var foreignClass = new SchoolClass { SchoolBranch = otherBranch, AcademicYear = year, GradeLevel = grade, Name = "5C" };
            var teacherUser = new User { Username = "teacher", FullName = "Teacher", PasswordHash = "test", Status = "ACTIVE", SchoolBranch = branch };
            var teacher = new Teacher { User = teacherUser };
            var subject = new Subject { Code = "MATH", Name = "Math" };
            db.AddRange(classA, classB, foreignClass, teacher, subject);
            await db.SaveChangesAsync();
            db.TeacherAssignments.Add(new TeacherAssignment { Teacher = teacher, SchoolClass = classA, Subject = subject, AssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var students = new StudentRepository(db, new PasswordHasher<User>());
            var classes = new ClassRepository(db);
            var schools = new SchoolRepository(db);
            var duplicateClass = await new ClassService(classes, schools).CreateClassAsync(school.Id,
                new(branch.Id, null, classA.Name, year.Id, grade.Id, null, null), default);
            Assert.Equal("VALIDATION_ERROR", duplicateClass.Error?.Code);
            Assert.Contains("name", duplicateClass.Error!.Details!.Keys);
            var created = await students.CreateStudentAsync(new(school.Id, "ST01", "Student One", null, "NAM", "ACTIVE", classA.Id), default);
            Assert.Equal(DirectoryWriteStatus.Success, created.Status);
            var studentId = created.Value;
            var student = await db.Students.Include(s => s.User).SingleAsync(s => s.Id == studentId);
            Assert.Equal("ST01", student.User.Username);
            Assert.False(string.IsNullOrEmpty(student.User.PasswordHash));
            Assert.Empty(await db.UserRoles.Where(r => r.UserId == student.UserId).ToArrayAsync());
            var duplicate = await students.CreateStudentAsync(new(school.Id, "ST01", "Duplicate", null, null, "ACTIVE", classB.Id), default);
            Assert.Equal(DirectoryWriteStatus.DuplicateStudentCode, duplicate.Status);
            Assert.Equal(DirectoryReadStatus.NotFound, (await students.GetStudentAsync(DirectoryScope.ForSchool(otherSchool.Id), studentId, default)).Status);
            Assert.Equal(DirectoryWriteStatus.ClassNotFound, (await students.CreateStudentAsync(new(school.Id, "ST02", "Wrong scope", null, null, "ACTIVE", foreignClass.Id), default)).Status);
            Assert.Single((await classes.ListClassesAsync(new(DirectoryScope.FromActor(teacherUser.Id, true), null, null, null, null, null, 1, 20), default)).Value!.Items);
            Assert.Equal(DirectoryWriteStatus.ClassHasActiveStudents, (await classes.DeactivateClassAsync(school.Id, classA.Id, default)).Status);

            Assert.Equal(DirectoryWriteStatus.Success, (await students.TransferStudentClassAsync(new(school.Id, studentId, classB.Id, null), default)).Status);
            var detail = (await students.GetStudentAsync(DirectoryScope.ForSchool(school.Id), studentId, default)).Value!;
            Assert.Equal(2, detail.History.Count);
            Assert.Equal(classB.Id, detail.History.First(h => h.EnrollmentStatus == "ACTIVE").ClassId);
            Assert.Equal(DirectoryWriteStatus.AlreadyEnrolledInClass, (await students.TransferStudentClassAsync(new(school.Id, studentId, classA.Id, null), default)).Status);
            var filtered = await students.ListStudentsAsync(new(DirectoryScope.ForSchool(school.Id), null, null, classB.Id, null, null, 1, 20, year.Id), default);
            Assert.Single(filtered.Value!.Items);
            Assert.Equal(classB.Id, filtered.Value.Items[0].ClassId);
            Assert.Equal(DirectoryWriteStatus.Success, (await classes.DeactivateClassAsync(school.Id, classA.Id, default)).Status);
            var references = (await schools.GetReferenceDataAsync(DirectoryScope.ForSchool(school.Id), year.Id, default)).Value!;
            Assert.DoesNotContain(references.Classes, c => c.Id == classA.Id);
            Assert.Contains(references.Classes, c => c.Id == classB.Id);
            Assert.False(string.IsNullOrWhiteSpace(await students.SuggestStudentCodeAsync(school.Id, classB.Id, default)));
            Assert.Equal(DirectoryWriteStatus.Success, (await students.DeactivateStudentAsync(school.Id, studentId, default)).Status);
            Assert.Equal("INACTIVE", student.User.Status);
            Assert.DoesNotContain(await db.StudentEnrollments.Where(e => e.StudentId == studentId).ToArrayAsync(), e => e.EndedOn == null);
            var generated = await students.CreateStudentAsync(new(school.Id, "", "Generated One", null, null, "ACTIVE", classB.Id), default);
            Assert.Equal(DirectoryWriteStatus.Success, generated.Status);
            var firstCode = (await db.Students.Include(s => s.User).SingleAsync(s => s.Id == generated.Value)).User.Username;
            Assert.False(string.IsNullOrWhiteSpace(firstCode));
            var next = await students.CreateStudentAsync(new(school.Id, "", "Generated Two", null, null, "ACTIVE", classB.Id), default);
            Assert.Equal(DirectoryWriteStatus.Success, next.Status);
            var secondCode = (await db.Students.Include(s => s.User).SingleAsync(s => s.Id == next.Value)).User.Username;
            Assert.NotEqual(firstCode, secondCode);
        }
        finally
        {
            Assert.StartsWith("telo_dbv2_merge_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
