using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Xunit;

namespace Infrastructure.Tests;

public sealed class ExamRoomRepositoryQueryTests
{
    [Fact]
    public void ListByExamQuery_TranslatesToMySqlAndOrdersBeforeProjection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Database=model_validation;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new ApplicationDbContext(options);

        var method = typeof(ExamRoomRepository).GetMethod(
            "BuildListByExamQuery",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var query = Assert.IsAssignableFrom<IQueryable>(method.Invoke(null, [context, (ulong)7]));
        var sql = query.ToQueryString();

        Assert.Contains("FROM `exam_rooms`", sql);
        Assert.Contains("ORDER BY `e`.`code`, `e`.`id`", sql);
        Assert.Contains("COUNT(*)", sql);
    }
}
