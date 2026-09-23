using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Models;
using Infrastructure.Repositories.Implement;
using Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class MatrixRepositoryIntegrationTests
{
    [Fact]
    public async Task MatrixRepository_QueriesDedicatedDatabaseWithPaging()
    {
        await using var context = CreateContext();
        var repository = new ExamMatrixRepository(context);

        var result = await repository.ListAsync(
            new MatrixListFilter(Page: 1, PageSize: 20),
            CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.InRange(result.Items.Count, 0, 20);
        Assert.True(result.TotalCount >= result.Items.Count);
    }

    [Fact]
    public async Task MatrixReferenceReader_RejectsUnknownAcademicContext()
    {
        await using var context = CreateContext();
        var reader = new MatrixReferenceRepository(context, new TestRoleCatalog());

        var exception = await Assert.ThrowsAsync<MatrixDomainException>(() =>
            reader.EnsureValidAsync(
                ulong.MaxValue,
                null,
                Array.Empty<ulong>(),
                CancellationToken.None));

        Assert.Equal("InvalidReference", exception.Code);
    }

    [Fact]
    public async Task MatrixRepository_PersistsDirectMatrixAndRejectsStaleStatusUpdate()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Infrastructure integration matrix",
                Status = MatrixStatusCodes.Draft,
                AcademicContextId = 9106,
                TotalScore = 10,
                Details = new List<MatrixDetail>
                {
                    new()
                    {
                        LessonId = 9107,
                        CognitiveLevel = "NHAN_BIET",
                        QuestionType = "MULTIPLE_CHOICE",
                        QuestionCount = 2,
                        Percentage = 1m
                    }
                }
            };

            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            Assert.NotEqual(0ul, matrix.Id);

            var loaded = await repository.GetAsync(matrix.Id, CancellationToken.None);
            Assert.NotNull(loaded);
            Assert.Single(loaded!.Details);
            Assert.Equal("Lesson 1", loaded.Details.Single().Lesson.Title);

            await using var concurrentContext = CreateContext();
            var concurrentRepository = new ExamMatrixRepository(concurrentContext);
            var concurrent = await concurrentRepository.GetAsync(matrix.Id, CancellationToken.None);
            Assert.NotNull(concurrent);

            loaded.Status = MatrixStatusCodes.Submitted;
            Assert.True(await repository.TryUpdateStatusAsync(
                loaded,
                MatrixStatusCodes.Draft,
                CancellationToken.None));

            concurrent!.Status = MatrixStatusCodes.Submitted;
            Assert.False(await concurrentRepository.TryUpdateStatusAsync(
                concurrent,
                MatrixStatusCodes.Draft,
                CancellationToken.None));

            await repository.DeleteAsync(loaded);
            await SaveAsync(context);

            Assert.Null(await repository.GetAsync(matrix.Id, CancellationToken.None));
            Assert.Equal(
                0,
                await context.MatrixDetails.CountAsync(
                    detail => detail.ExamMatrixId == matrix.Id));
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_AttachesExistingTaskNavigationBeforeAddingDelegatedMatrix()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var task = await context.WorkTasks
                .AsNoTracking()
                .SingleAsync(item => item.Id == 9112);
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Delegated navigation integration matrix",
                Status = MatrixStatusCodes.Draft,
                TaskId = task.Id,
                Task = task,
                AcademicContextId = 9106,
                TotalScore = 10
            };

            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            Assert.NotEqual(0ul, matrix.Id);
            Assert.Equal(
                1,
                await context.ExamMatrices.CountAsync(item => item.TaskId == task.Id));
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_MapsDuplicateCellConstraintToApplicationConflict()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Duplicate cell integration matrix",
                Status = MatrixStatusCodes.Draft,
                AcademicContextId = 9106,
                TotalScore = 10,
                Details = new List<MatrixDetail>
                {
                    new()
                    {
                        LessonId = 9107,
                        CognitiveLevel = "NHAN_BIET",
                        QuestionType = "MULTIPLE_CHOICE",
                        QuestionCount = 1,
                        Percentage = 1m
                    }
                }
            };

            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            context.MatrixDetails.Add(new MatrixDetail
            {
                ExamMatrixId = matrix.Id,
                LessonId = 9107,
                CognitiveLevel = "NHAN_BIET",
                QuestionType = "MULTIPLE_CHOICE",
                QuestionCount = 1,
                Percentage = 1m
            });

            var exception = await Assert.ThrowsAsync<MatrixDomainException>(() =>
                SaveAsync(context));

            Assert.Equal("DuplicateDetail", exception.Code);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_MapsDuplicateTaskConstraintToApplicationConflict()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var firstRepository = new ExamMatrixRepository(context);
            var firstMatrix = new ExamMatrix
            {
                Name = "First delegated matrix",
                Status = MatrixStatusCodes.Draft,
                TaskId = 9112,
                AcademicContextId = 9106,
                TotalScore = 10
            };

            await firstRepository.AddAsync(firstMatrix, CancellationToken.None);
            await SaveAsync(context);

            await using var secondContext = CreateContext();
            var secondRepository = new ExamMatrixRepository(secondContext);
            var secondMatrix = new ExamMatrix
            {
                Name = "Second delegated matrix",
                Status = MatrixStatusCodes.Draft,
                TaskId = 9112,
                AcademicContextId = 9106,
                TotalScore = 10
            };

            await secondRepository.AddAsync(secondMatrix, CancellationToken.None);
            var exception = await Assert.ThrowsAsync<MatrixDomainException>(() =>
                SaveAsync(secondContext));

            Assert.Equal("TaskAlreadyHasMatrix", exception.Code);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_LocksByStatusUpdatesTaskStatusAndReplacesSameCellDetails()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Lock and replace matrix",
                Status = MatrixStatusCodes.Draft,
                AcademicContextId = 9106,
                TotalScore = 10,
                Details = new List<MatrixDetail>
                {
                    new()
                    {
                        LessonId = 9107,
                        CognitiveLevel = "NHAN_BIET",
                        QuestionType = "MULTIPLE_CHOICE",
                        QuestionCount = 2,
                        Percentage = 1m
                    }
                }
            };
            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            await using var transaction = await context.Database.BeginTransactionAsync();
            Assert.True(await repository.LockWithStatusAsync(
                matrix.Id, MatrixStatusCodes.Draft, CancellationToken.None));
            Assert.False(await repository.LockWithStatusAsync(
                matrix.Id, MatrixStatusCodes.Submitted, CancellationToken.None));
            await transaction.RollbackAsync();

            // Replace the only detail with a detail for the very same cell.
            var loaded = await repository.GetAsync(matrix.Id, CancellationToken.None);
            loaded!.ReplaceDetails(
                new[] { new MatrixDetailValue(9107, "NHAN_BIET", "MULTIPLE_CHOICE", 5, 10m) },
                new MatrixActor(1, MatrixActorRole.Pht, 9103));
            await SaveAsync(context);

            await using var verify = CreateContext();
            var stored = await verify.MatrixDetails
                .SingleAsync(detail => detail.ExamMatrixId == matrix.Id);
            Assert.Equal(5u, stored.QuestionCount);

            await repository.SetTaskStatusAsync(
                9112, "SUBMITTED", 9110, CancellationToken.None);
            var task = await verify.WorkTasks.AsNoTracking().SingleAsync(item => item.Id == 9112);
            Assert.Equal("SUBMITTED", task.Status);
            Assert.Equal(9110ul, task.UpdatedByUserId);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_PersistsRejectionTogetherWithTheStatus()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Rejected matrix",
                Status = MatrixStatusCodes.Submitted,
                AcademicContextId = 9106,
                TotalScore = 10
            };
            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            matrix.Reject(
                new MatrixActor(9110, MatrixActorRole.Pht, 9103),
                "Sửa lại câu 3",
                DateTime.UtcNow);
            Assert.True(await repository.TryUpdateStatusAsync(
                matrix,
                MatrixStatusCodes.Submitted,
                CancellationToken.None));

            await using var verify = CreateContext();
            var stored = await verify.ExamMatrices
                .AsNoTracking()
                .SingleAsync(item => item.Id == matrix.Id);
            Assert.Equal(MatrixStatusCodes.Draft, stored.Status);
            Assert.Equal("Sửa lại câu 3", stored.RejectComment);
            Assert.Equal(9110ul, stored.RejectedByUserId);
            Assert.NotNull(stored.RejectedAt);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_StoresTheAuthor()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var first = new ExamMatrix
            {
                Name = "Author one",
                Status = MatrixStatusCodes.Draft,
                AcademicContextId = 9106,
                TotalScore = 10,
                CreatedByUserId = 9110
            };

            await repository.AddAsync(first, CancellationToken.None);
            await SaveAsync(context);

            Assert.NotEqual(default, first.CreatedAt);

            await using var verify = CreateContext();
            var stored = await verify.ExamMatrices.AsNoTracking().SingleAsync(item => item.Id == first.Id);
            Assert.Equal(9110ul, stored.CreatedByUserId);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_PersistsApprovalTogetherWithTheStatus()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);
            var matrix = new ExamMatrix
            {
                Name = "Approved matrix",
                Status = MatrixStatusCodes.Submitted,
                AcademicContextId = 9106,
                TotalScore = 10
            };
            await repository.AddAsync(matrix, CancellationToken.None);
            await SaveAsync(context);

            var at = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);
            matrix.Approve(new MatrixActor(9110, MatrixActorRole.Pht, 9103), at);
            Assert.True(await repository.TryUpdateStatusAsync(
                matrix,
                MatrixStatusCodes.Submitted,
                CancellationToken.None));

            await using var verify = CreateContext();
            var stored = await verify.ExamMatrices
                .AsNoTracking()
                .SingleAsync(item => item.Id == matrix.Id);
            Assert.Equal(MatrixStatusCodes.Approved, stored.Status);
            Assert.Equal(9110ul, stored.ApprovedByUserId);
            Assert.Equal(at, stored.ApprovedAt);
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    [Fact]
    public async Task MatrixRepository_GetPeopleReturnsNamesAndIgnoresUnknownIds()
    {
        await using var context = CreateContext();
        await SeedReferencesAsync(context);
        await SeedMatrixTaskAsync(context);

        try
        {
            var repository = new ExamMatrixRepository(context);

            var people = await repository.GetPeopleAsync(new ulong[] { 9110, ulong.MaxValue }, CancellationToken.None);

            var person = Assert.Single(people).Value;
            Assert.Equal(9110ul, person.UserId);
            Assert.Equal("Matrix Integration Creator", person.FullName);
            Assert.Empty(await repository.GetPeopleAsync(Array.Empty<ulong>(), CancellationToken.None));
        }
        finally
        {
            await CleanupReferencesAsync(context);
        }
    }

    private static Task<int> SaveAsync(ApplicationDbContext context) =>
        new Infrastructure.UnitOfWork.UnitOfWork(context, null!, null!, null!, null!, null!).CompleteAsync();

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

    private static async Task SeedReferencesAsync(ApplicationDbContext context)
    {
        await CleanupReferencesAsync(context);

        var statements = new[]
        {
            "INSERT INTO academic_years (id, name, start_date, end_date, status) VALUES (9101, 'Integration Year', '2026-01-01', '2026-12-31', 'ACTIVE')",
            "INSERT INTO schools (id, code, name, status) VALUES (9102, 'INT-SCHOOL', 'Integration School', 'ACTIVE')",
            "INSERT INTO school_branches (id, school_id, code, name, status) VALUES (9103, 9102, 'INT-BRANCH', 'Integration Branch', 'ACTIVE')",
            "INSERT INTO grade_levels (id, name, status) VALUES (9104, 'Integration Grade', 'ACTIVE')",
            "INSERT INTO subjects (id, name, status) VALUES (9105, 'Integration Subject', 'ACTIVE')",
            "INSERT INTO textbooks (id, title, book_set) VALUES (9108, 'Integration Textbook', NULL)",
            "INSERT INTO textbook_chapters (id, textbook_id, title, sort_order) VALUES (9109, 9108, 'Chapter 1', 1)",
            "INSERT INTO textbook_lessons (id, chapter_id, content, title, sort_order) VALUES (9107, 9109, 'Content', 'Lesson 1', 1)",
            "INSERT INTO academic_contexts (id, academic_year_id, school_id, textbook_id, subject_id, grade_level_id, school_branch_id) VALUES (9106, 9101, 9102, 9108, 9105, 9104, 9103)"
        };

        foreach (var statement in statements)
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task SeedMatrixTaskAsync(ApplicationDbContext context)
    {
        var statements = new[]
        {
            "INSERT INTO users (id, username, email, password_hash, full_name, status) VALUES (9110, 'matrix-int-creator', 'matrix-int-creator@example.test', 'hash', 'Matrix Integration Creator', 'ACTIVE')",
            "INSERT INTO users (id, username, email, password_hash, full_name, status) VALUES (9111, 'matrix-int-assignee', 'matrix-int-assignee@example.test', 'hash', 'Matrix Integration Assignee', 'ACTIVE')",
            "INSERT INTO tasks (id, created_by_user_id, assigned_to_user_id, due_at, status, description, created_at, updated_at, updated_by_user_id, task_type, academic_context_id, semester_id) VALUES (9112, 9110, 9111, NULL, 'PENDING', 'Integration matrix task', CURRENT_TIMESTAMP(6), NULL, NULL, 'MATRIX', 9106, NULL)"
        };

        foreach (var statement in statements)
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }
    }

    private static async Task CleanupReferencesAsync(ApplicationDbContext context)
    {
        var statements = new[]
        {
            "DELETE FROM matrix_details WHERE exam_matrix_id IN (SELECT id FROM exam_matrices WHERE academic_context_id = 9106)",
            "DELETE FROM exam_matrices WHERE academic_context_id = 9106",
            "DELETE FROM tasks WHERE id = 9112",
            "DELETE FROM users WHERE id IN (9110, 9111)",
            "DELETE FROM academic_contexts WHERE id = 9106",
            "DELETE FROM textbook_lessons WHERE id = 9107",
            "DELETE FROM textbook_chapters WHERE id = 9109",
            "DELETE FROM textbooks WHERE id = 9108",
            "DELETE FROM school_branches WHERE id = 9103",
            "DELETE FROM schools WHERE id = 9102",
            "DELETE FROM subjects WHERE id = 9105",
            "DELETE FROM grade_levels WHERE id = 9104",
            "DELETE FROM academic_years WHERE id = 9101"
        };

        foreach (var statement in statements)
        {
            await context.Database.ExecuteSqlRawAsync(statement);
        }
    }
}
