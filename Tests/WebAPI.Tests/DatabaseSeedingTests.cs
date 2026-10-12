using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace WebAPI.Tests;

public sealed class DatabaseSeedingTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ApiStartsWithoutDatabaseAndDoesNotExposeTestSeeder(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("ConnectionStrings:DefaultConnection",
                    "Server=127.0.0.1;Port=1;Database=unused;User=unused;Password=unused;Connection Timeout=1;");
                builder.UseSetting("Jwt:SigningKey",
                    "database-seeding-test-signing-key-at-least-32-chars");
                builder.UseSetting("NotificationEmail:Enabled", "false");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });
        using var client = factory.CreateClient();
        client.BaseAddress = new Uri("https://localhost");

        using var response = await client.PostAsync("/api/auth/seed-test", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
