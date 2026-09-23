using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Tests.Persistence;

internal static class ModelFactory
{
    internal static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=localhost;Database=sep_contract;User=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        using var context = new ApplicationDbContext(options);
        return context.Model;
    }

    internal static IModel CreateDesignModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=localhost;Database=sep_contract;User=root;",
                new MySqlServerVersion(new Version(8, 0, 0))).Options;
        using var context = new ApplicationDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
