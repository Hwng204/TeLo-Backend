using Application.Services.Interface;
using Domain.Entities.Academic;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

public sealed class AcademicYearActivationIntegrationTests
{
    [IdentityMySqlFact]
    public async Task SwitchingYearClosesPreviousYearAtomicallyAndKeepsSingleActiveYear()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!);
        var database = "telo_year_switch_test_" + Guid.NewGuid().ToString("N");
        connection.Database = database;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var previous = Year(2025);
            previous.Activate();
            var target = Year(2026);
            var invalid = Year(2028);
            invalid.Semesters.Clear();
            invalid.Status = "CLOSED";
            var raceA = Year(2029);
            var raceB = Year(2030);
            db.AcademicYears.AddRange(previous, target, invalid, raceA, raceB);
            await db.SaveChangesAsync();

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "academic-year-integration-tests-signing-key-32-characters");
            });
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IAcademicYearService>();
                var result = await service.ActivateAsync(target.Id, CancellationToken.None);
                Assert.True(result.IsSuccess);
                var failure = await service.ActivateAsync(invalid.Id, CancellationToken.None);
                Assert.Equal("ACADEMIC_YEAR_CLOSED", failure.Error?.Code);
            }
            db.ChangeTracker.Clear();
            // Semesters have no status and years no version any more (D2): only the year status switches.
            Assert.Equal("CLOSED", (await db.AcademicYears.SingleAsync(year => year.Id == previous.Id)).Status);
            Assert.Equal(target.Id, (await db.AcademicYears.SingleAsync(year => year.Status == "ACTIVE")).Id);

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var repositoryA = new AcademicYearRepository(contextA);
            var repositoryB = new AcademicYearRepository(contextB);
            var a = (await repositoryA.GetByIdWithSemestersAsync(raceA.Id, CancellationToken.None))!;
            var b = (await repositoryB.GetByIdWithSemestersAsync(raceB.Id, CancellationToken.None))!;
            var outcomes = await Task.WhenAll(repositoryA.TryActivateAsync(a, CancellationToken.None), repositoryB.TryActivateAsync(b, CancellationToken.None));
            Assert.Contains(true, outcomes);
            db.ChangeTracker.Clear();
            var active = Assert.Single(await db.AcademicYears.Where(year => year.Status == "ACTIVE").ToArrayAsync());
            Assert.Contains(active.Id, new[] { raceA.Id, raceB.Id });
            Assert.Equal("CLOSED", (await db.AcademicYears.SingleAsync(year => year.Id == target.Id)).Status);
        }
        finally
        {
            Assert.StartsWith("telo_year_switch_test_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static AcademicYear Year(int first)
    {
        return new AcademicYear
        {
            Name = $"{first}-{first + 1}",
            Semesters = [new() { Order = 1, Name = "Học kỳ I" }, new() { Order = 2, Name = "Học kỳ II" }]
        };
    }
}
