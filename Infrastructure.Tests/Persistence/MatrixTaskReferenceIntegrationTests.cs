using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Implement;
using Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class MatrixTaskReferenceIntegrationTests
{
    [Fact]
    public async Task ReferenceReaderReturnsSameBranchDataAndRejectsCrossBranchContext()
    {
        await using var context = CreateContext();
        await SeedAsync(context);

        try
        {
            var reader = new MatrixReferenceRepository(context, new TestRoleCatalog());
            await reader.EnsureAssignmentValidAsync(
                new MatrixActor(9210, MatrixActorRole.Pht),
                9211,
                9206,
                null,
                CancellationToken.None);

            var result = await reader.GetReferenceDataAsync(
                new MatrixActor(9210, MatrixActorRole.Pht),
                9206,
                CancellationToken.None);

            Assert.Single(result.AcademicContexts);
            Assert.Single(result.Lessons);
            Assert.Single(result.TeamLeads);
            Assert.Equal((ulong)9211, result.TeamLeads[0].Id);

            var exception = await Assert.ThrowsAsync<MatrixDomainException>(() =>
                reader.GetReferenceDataAsync(
                    new MatrixActor(9210, MatrixActorRole.Pht),
                    9216,
                    CancellationToken.None));

            Assert.Equal("Forbidden", exception.Code);
            Assert.Equal("Ngữ cảnh học thuật thuộc chi nhánh khác.", exception.Message);

            var repository = new MatrixTaskRepository(context);
            var task = new WorkTask
            {
                Id = 9214,
                CreatedByUserId = 9210,
                AssignedToUserId = 9211,
                Status = "ASSIGNED",
                TaskType = "MATRIX",
                AcademicContextId = 9206,
                CreatedAt = DateTime.UtcNow
            };
            await repository.AddAsync(task, CancellationToken.None);
            await new Infrastructure.UnitOfWork.UnitOfWork(context, null!, null!, null!, null!, null!).CompleteAsync();

            var page = await repository.ListAsync(
                new MatrixTaskFilter(Page: 1, PageSize: 20, AssignedToUserId: 9211),
                CancellationToken.None);

            Assert.Single(page.Items);
            Assert.Equal((ulong)9214, page.Items[0].Id);

            // Bộ lọc của danh sách nhiệm vụ: theo ngữ cảnh, theo mã (suy ra từ id) và theo yêu cầu công việc.
            task.Description = "Lap ma tran giua ky";
            await new Infrastructure.UnitOfWork.UnitOfWork(context, null!, null!, null!, null!, null!).CompleteAsync();

            async Task<int> CountAsync(MatrixTaskFilter filter) =>
                (await repository.ListAsync(filter, CancellationToken.None)).Items.Count;

            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, AcademicContextId: 9206)));
            Assert.Equal(0, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, AcademicContextId: 9207)));
            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, Keyword: "giua ky")));
            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, Keyword: "NV-MT-9214")));
            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, Keyword: "9214")));
            Assert.Equal(0, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, Keyword: "khong-co-gi")));

            // Từng chiều của ngữ cảnh lọc độc lập, không cần đủ bốn chiều.
            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, AcademicYearId: 9201)));
            Assert.Equal(0, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, AcademicYearId: 9299)));
            Assert.Equal(1, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, SubjectId: 9205, GradeLevelId: 9204)));
            Assert.Equal(0, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, GradeLevelId: 9299)));
            // Nhiệm vụ không chọn học kỳ thì không khớp khi lọc theo học kỳ.
            Assert.Equal(0, await CountAsync(new MatrixTaskFilter(AssignedToUserId: 9211, SemesterId: 1)));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MatrixTest");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__MatrixTest must be configured for matrix integration tests.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        await CleanupAsync(context);
        var statements = new[]
        {
            "INSERT INTO academic_years (id, name, start_date, end_date, status) VALUES (9201, 'Reference Year', '2026-01-01', '2026-12-31', 'ACTIVE')",
            "INSERT INTO schools (id, code, name, status) VALUES (9202, 'REF-SCHOOL', 'Reference School', 'ACTIVE')",
            "INSERT INTO school_branches (id, school_id, code, name, status) VALUES (9203, 9202, 'REF-BRANCH', 'Reference Branch', 'ACTIVE')",
            "INSERT INTO school_branches (id, school_id, code, name, status) VALUES (9215, 9202, 'REF-BRANCH-OTHER', 'Other Reference Branch', 'ACTIVE')",
            "INSERT INTO grade_levels (id, name, status) VALUES (9204, 'Reference Grade', 'ACTIVE')",
            "INSERT INTO subjects (id, name, status) VALUES (9205, 'Reference Subject', 'ACTIVE')",
            "INSERT INTO textbooks (id, title, book_set) VALUES (9208, 'Reference Textbook', NULL)",
            "INSERT INTO textbook_chapters (id, textbook_id, title, sort_order) VALUES (9209, 9208, 'Reference Chapter', 1)",
            "INSERT INTO textbook_lessons (id, chapter_id, content, title, sort_order) VALUES (9207, 9209, 'Reference Content', 'Reference Lesson', 1)",
            "INSERT INTO academic_contexts (id, academic_year_id, school_id, textbook_id, subject_id, grade_level_id, school_branch_id) VALUES (9206, 9201, 9202, 9208, 9205, 9204, 9203)",
            "INSERT INTO academic_contexts (id, academic_year_id, school_id, textbook_id, subject_id, grade_level_id, school_branch_id) VALUES (9216, 9201, 9202, 9208, 9205, 9204, 9215)",
            "INSERT INTO roles (id, code, name) VALUES (9212, 'TEAM_LEAD', 'Team Lead')",
            "INSERT INTO users (id, username, email, school_branch_id, password_hash, full_name, status) VALUES (9210, 'reference-pht', 'reference-pht@example.test', 9203, 'hash', 'Reference PHT', 'ACTIVE')",
            "INSERT INTO users (id, username, email, school_branch_id, password_hash, full_name, status) VALUES (9211, 'reference-lead', 'reference-lead@example.test', 9203, 'hash', 'Reference Lead', 'ACTIVE')",
            "INSERT INTO user_roles (id, user_id, role_id) VALUES (9213, 9211, 9212)"
        };

        foreach (var statement in statements)
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        var statements = new[]
        {
            "DELETE FROM tasks WHERE id = 9214",
            "DELETE FROM user_roles WHERE id = 9213",
            "DELETE FROM users WHERE id IN (9210, 9211)",
            "DELETE FROM roles WHERE id = 9212",
            "DELETE FROM academic_contexts WHERE id IN (9206, 9216)",
            "DELETE FROM textbook_lessons WHERE id = 9207",
            "DELETE FROM textbook_chapters WHERE id = 9209",
            "DELETE FROM textbooks WHERE id = 9208",
            "DELETE FROM school_branches WHERE id IN (9203, 9215)",
            "DELETE FROM schools WHERE id = 9202",
            "DELETE FROM subjects WHERE id = 9205",
            "DELETE FROM grade_levels WHERE id = 9204",
            "DELETE FROM academic_years WHERE id = 9201"
        };

        foreach (var statement in statements)
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }
    }
}
