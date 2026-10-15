using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class DbV2MergeModelTests
{
    [Fact]
    public void MergeKeepsTheDbV2SchemaAndKnownMigrations()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=unused;User=unused;Password=unused;", new MySqlServerVersion(new Version(8, 0, 0)))
            .Options);
        var entities = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(52, entities.Select(e => e.GetTableName()).Distinct().Count());
        Assert.DoesNotContain(entities, e => e.GetTableName() is "tasks" or "questions" or "exam_task_papers" or "question_package_submissions");
        Assert.DoesNotContain(entities.SelectMany(e => e.GetProperties()), p => p.Name is "Version" or "QuestionVersion");
        Assert.Equal(new[] { "20261015020254_InitialCreate", "20261015114344_AddChapterLessonNumber" }, db.GetService<IMigrationsAssembly>().Migrations.Keys);
    }
}
