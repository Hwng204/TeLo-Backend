using Infrastructure.Context;
using Infrastructure.Exports;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Infrastructure.Security;
using Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:DefaultConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured before registering Infrastructure.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 0)),
                mysql => mysql.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.GetName().Name)));

        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<IMatrixRepository, ExamMatrixRepository>();
        services.AddScoped<IMatrixTaskRepository, MatrixTaskRepository>();
        services.AddScoped<IMatrixReferenceRepository, MatrixReferenceRepository>();
        services.AddScoped<ISchoolDirectoryRepository, SchoolDirectoryRepository>();
        services.AddScoped<ISchoolDirectoryAdminRepository, SchoolDirectoryAdminRepository>();
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMatrixRoleCatalog, ConfiguredMatrixRoleCatalog>();
        services.AddSingleton<IMatrixWorkbookExporter, ClosedXmlMatrixWorkbookExporter>();

        return services;
    }
}
