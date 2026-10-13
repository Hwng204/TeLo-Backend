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
            var staleTarget = Year(2027);
            var invalid = Year(2028);
            invalid.Semesters.Clear();
            var raceA = Year(2029);
            var raceB = Year(2030);
            db.AcademicYears.AddRange(previous, target, staleTarget, invalid, raceA, raceB);
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
                Assert.Equal("INCOMPLETE_TERMS", failure.Error?.Code);
            }
            db.ChangeTracker.Clear();
            var ended = await db.AcademicYears.Include(year => year.Semesters).SingleAsync(year => year.Id == previous.Id);
            Assert.Equal("CLOSED", ended.Status);
            Assert.All(ended.Semesters, term => Assert.Equal("CLOSED", term.Status));
            Assert.Equal(target.Id, (await db.AcademicYears.SingleAsync(year => year.Status == "ACTIVE")).Id);

            // Force a stale target after loading it: the second save must fail and
            // roll back the first save that temporarily closed the active year.
            await using (var staleContext = new ApplicationDbContext(options))
            {
                var repository = new AcademicYearRepository(staleContext);
                var stale = (await repository.GetByIdWithSemestersAsync(staleTarget.Id, CancellationToken.None))!;
                await staleContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_years SET version = version + 1 WHERE id = {staleTarget.Id}");
                Assert.False(await repository.TryActivateAsync(stale, CancellationToken.None));
            }
            db.ChangeTracker.Clear();
            Assert.Equal("DRAFT", (await db.AcademicYears.SingleAsync(year => year.Id == staleTarget.Id)).Status);
            var stillActive = await db.AcademicYears.Include(year => year.Semesters).SingleAsync(year => year.Id == target.Id);
            Assert.Equal("ACTIVE", stillActive.Status);
            Assert.Equal("ACTIVE", stillActive.Semesters.Single(term => term.Order == 1).Status);

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
        var year = new AcademicYear
        {
            Name = $"{first}-{first + 1}", StartDate = new(first, 8, 15), EndDate = new(first + 1, 5, 31),
            Semesters = [
                new() { Order = 1, Name = "Học kỳ I", StartDate = new(first, 8, 15), EndDate = new(first + 1, 1, 15) },
                new() { Order = 2, Name = "Học kỳ II", StartDate = new(first + 1, 1, 16), EndDate = new(first + 1, 5, 31) }
            ]
        };
        year.AssignCode($"AY_{first}");
        return year;
    }
}
