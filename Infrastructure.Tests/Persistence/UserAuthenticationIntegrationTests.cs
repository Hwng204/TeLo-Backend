using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class UserAuthenticationIntegrationTests
{
    private const string Password = "Test@12345";

    [Fact]
    public async Task AuthenticateAsync_AcceptsAnyActiveUserAndReturnsEveryRoleCode()
    {
        await using var context = CreateContext();
        await CleanupAsync(context);
        await SeedAsync(context);

        try
        {
            var service = new UserAuthenticationService(context, new PasswordHasher<User>());

            var user = await service.AuthenticateAsync(
                new LoginRequest("login-test-teacher", Password),
                CancellationToken.None);

            Assert.NotNull(user);
            Assert.Equal(9301ul, user!.UserId);
            Assert.Equal(new[] { "GIAO_VIEN_TEST", "TO_TRUONG_TEST" }, user.RoleCodes.Order().ToArray());

            var wrongPassword = await service.AuthenticateAsync(
                new LoginRequest("login-test-teacher", "wrong"),
                CancellationToken.None);
            Assert.Null(wrongPassword);

            var unknown = await service.AuthenticateAsync(
                new LoginRequest("login-test-nobody", Password),
                CancellationToken.None);
            Assert.Null(unknown);

            var inactive = await service.AuthenticateAsync(
                new LoginRequest("login-test-inactive", Password),
                CancellationToken.None);
            Assert.Null(inactive);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        var hash = new PasswordHasher<User>().HashPassword(new User(), Password);

        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO users (id, username, email, password_hash, full_name, status) VALUES
            (9301, 'login-test-teacher', 'login-test-teacher@example.test', {hash}, 'Login Test Teacher', 'ACTIVE'),
            (9302, 'login-test-inactive', 'login-test-inactive@example.test', {hash}, 'Login Test Inactive', 'INACTIVE')");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO roles (id, code, name) VALUES (9301, 'GIAO_VIEN_TEST', 'Giao vien test'), (9302, 'TO_TRUONG_TEST', 'To truong test')");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO user_roles (user_id, role_id) VALUES (9301, 9301), (9301, 9302), (9302, 9301)");
    }

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("DELETE FROM user_roles WHERE user_id IN (9301, 9302)");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM roles WHERE id IN (9301, 9302)");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM users WHERE id IN (9301, 9302)");
    }

    private static ApplicationDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MatrixTest");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__MatrixTest must be configured for integration tests.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new ApplicationDbContext(options);
    }
}
