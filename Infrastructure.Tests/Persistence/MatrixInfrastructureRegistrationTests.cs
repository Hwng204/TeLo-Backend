using Infrastructure;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Tests.Persistence;

public sealed class MatrixInfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersMatrixRepositoriesAndUnitOfWork()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:DefaultConnection"] =
            "Server=localhost;Database=sep_contract;User=root;";

        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMatrixRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMatrixTaskRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMatrixReferenceRepository>());

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.NotNull(unitOfWork.Matrices);
        Assert.NotNull(unitOfWork.MatrixTasks);
        Assert.NotNull(unitOfWork.MatrixReferences);
    }
}
