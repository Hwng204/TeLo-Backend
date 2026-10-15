using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

// AddChapterLessonNumber over InitialCreate data on a throwaway MySQL database, then the archive rules on the real
// indexes: only ACTIVE chapters and lessons must have a unique number and title.
public sealed class ChapterLessonNumberMigrationTests
{
    [IdentityMySqlFact]
    public async Task MigrationNumbersExistingRows_AndArchivedRowsFreeTheirNumberAndTitle()
    {
        var database = "telo_chapter_number_" + Guid.NewGuid().ToString("N");
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!) { Database = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261015020254_InitialCreate");
            var branch = new SchoolBranch { School = new School { Code = "NUM", Name = "Number school" }, Code = "A", Name = "Branch A" };
            var grade = new GradeLevel { Name = "Grade 5" };
            var otherGrade = new GradeLevel { Name = "Grade 6" };
            var subject = new Subject { Code = "MATH", Name = "Math" };
            var user = new User { Username = "pht", FullName = "PHT", PasswordHash = "test", Status = "ACTIVE", SchoolBranch = branch };
            db.AddRange(grade, otherGrade, subject, user);
            await db.SaveChangesAsync();

            // The model already maps number/active_flag, so old-schema chapters and lessons go in as SQL, in id order.
            foreach (var (gradeId, title) in new[] { (grade.Id, "Chương A"), (otherGrade.Id, "Chương B"), (grade.Id, "Chương C") })
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO chapter (branch_id, grade_level_id, subject_id, title, status) VALUES ({branch.Id}, {gradeId}, {subject.Id}, {title}, 'ACTIVE')");
            }

            foreach (var (chapter, title) in new[] { ("Chương A", "Bài 1"), ("Chương C", "Bài X"), ("Chương A", "Bài 2") })
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO lesson (chapter_id, title, content, status) SELECT chapter_id, {title}, '', 'ACTIVE' FROM chapter WHERE title = {chapter}");
            }

            await migrator.MigrateAsync();

            Assert.Equal(new uint[] { 1, 1, 2 }, await db.Chapters.OrderBy(c => c.Id).Select(c => c.Number).ToListAsync());
            Assert.Equal(new uint[] { 1, 1, 2 }, await db.Lessons.OrderBy(l => l.Id).Select(l => l.Number).ToListAsync());

            var chapterA = await db.Chapters.SingleAsync(c => c.Title == "Chương A");
            var chapterB = await db.Chapters.SingleAsync(c => c.Title == "Chương B");
            var chapterC = await db.Chapters.SingleAsync(c => c.Title == "Chương C");
            var lesson1 = await db.Lessons.SingleAsync(l => l.Title == "Bài 1");
            var lessonX = await db.Lessons.SingleAsync(l => l.Title == "Bài X");
            db.ExamMatrices.Add(new ExamMatrix
            {
                Name = "Ma trận", Status = MatrixStatusCodes.Draft, BranchId = branch.Id, SubjectId = subject.Id,
                GradeLevelId = grade.Id, TotalScore = 10, CreatedByUserId = user.Id,
                Details =
                {
                    new MatrixDetail { LessonId = lesson1.Id, CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 1, Percentage = 50 },
                    new MatrixDetail { LessonId = lessonX.Id, CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 1, Percentage = 50 }
                }
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var repository = new CurriculumRepository(db);
            var service = new CurriculumService(repository);
            var actor = new CurriculumActor(user.Id, true);

            // In use: archived, and its still-ACTIVE lessons leave with it. Not in use: deleted.
            Assert.True((await service.DeleteChapterAsync(actor, chapterA.Id, default)).Value!.Archived);
            Assert.Equal(CurriculumErrorCodes.LessonNotFound, (await service.DeleteLessonAsync(actor, lesson1.Id, default)).Error!.Code);
            Assert.True((await service.DeleteLessonAsync(actor, lessonX.Id, default)).Value!.Archived);
            Assert.False((await service.DeleteChapterAsync(actor, chapterB.Id, default)).Value!.Archived);
            Assert.False(await db.Chapters.AnyAsync(c => c.Id == chapterB.Id));
            var listed = Assert.Single(await repository.ListChaptersAsync(branch.Id, default));
            Assert.Equal((chapterC.Id, true), (listed.Id, listed.InUse));
            Assert.Empty(listed.Lessons);

            // The archived chapter and lesson free their number and title for new ACTIVE rows, once.
            var recreated = await service.CreateChapterAsync(actor, new SaveChapterRequest(grade.Id, subject.Id, 1, "Chương A"), default);
            Assert.Equal(1U, recreated.Value!.Number);
            var duplicate = await service.CreateChapterAsync(actor, new SaveChapterRequest(grade.Id, subject.Id, 1, "chương a"), default);
            Assert.Equal(new[] { "number", "title" }, duplicate.Error!.Details!.Keys.Order());
            Assert.Null((await service.CreateLessonAsync(actor, chapterC.Id, new SaveLessonRequest(1, "Bài X"), default)).Error);
            Assert.Equal(CurriculumErrorCodes.LessonDuplicate,
                (await service.CreateLessonAsync(actor, chapterC.Id, new SaveLessonRequest(1, "Bài Y"), default)).Error!.Code);
            Assert.Null((await service.CreateLessonAsync(actor, recreated.Value.Id, new SaveLessonRequest(2, "Bài mới"), default)).Error);

            // The unique indexes themselves reject a second ACTIVE row with a taken number or title.
            object[] duplicates =
            [
                new Chapter { SchoolBranchId = branch.Id, GradeLevelId = grade.Id, SubjectId = subject.Id, Number = 1, Title = "Khác" },
                new Chapter { SchoolBranchId = branch.Id, GradeLevelId = grade.Id, SubjectId = subject.Id, Number = 9, Title = "Chương A" },
                new Lesson { ChapterId = chapterC.Id, Number = 1, Title = "Khác" },
                new Lesson { ChapterId = chapterC.Id, Number = 9, Title = "bài x" }
            ];
            foreach (var row in duplicates)
            {
                db.Add(row);
                var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, Assert.IsType<MySqlException>(rejected.InnerException).ErrorCode);
                db.ChangeTracker.Clear();
            }

            // New matrices only offer ACTIVE lessons of ACTIVE chapters, ordered and coded by number.
            var references = new MatrixReferenceRepository(db, new ConfiguredMatrixRoleCatalog(new ConfigurationBuilder().Build()));
            var model = await references.GetReferenceDataAsync(
                new MatrixActor(user.Id, MatrixActorRole.Pht, null, true),
                new MatrixScope(branch.Id, subject.Id, grade.Id).Key,
                default);
            Assert.Equal(
                new[] { ("1", "2", 2U, "Bài mới"), ("2", "1", 1U, "Bài X") },
                model.Lessons.Select(l => (l.ChapterCode, l.Code, l.SortOrder, l.Title)));
        }
        finally
        {
            Assert.StartsWith("telo_chapter_number_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
