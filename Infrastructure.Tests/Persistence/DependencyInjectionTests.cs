using System.Reflection;
using Infrastructure;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Tests.Persistence;

public sealed class DependencyInjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_RejectsMissingConnectionString(string? value)
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] = value;

        Action action = () =>
        {
            new ServiceCollection().AddInfrastructure(configuration);
        };

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_UsesMySql80ServerVersion()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Server=localhost;Database=sep_contract;User=root;";

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        var mysqlExtension = options.Extensions.Single(extension =>
            extension.GetType().Name == "MySqlOptionsExtension");
        var serverVersion = mysqlExtension.GetType()
            .GetProperty("ServerVersion", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mysqlExtension);
        var version = serverVersion?.GetType()
            .GetProperty("Version", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(serverVersion) as Version;

        Assert.Equal(new Version(8, 0, 0), version);
    }
}
