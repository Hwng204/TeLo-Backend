using Infrastructure.Context;
using Infrastructure.External.Provinces;
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
        services.AddScoped<IProvinceRepository, ProvinceRepository>();
        services.AddScoped<IMatrixRepository, ExamMatrixRepository>();
        services.AddScoped<IMatrixTaskRepository, MatrixTaskRepository>();
        services.AddScoped<IMatrixReferenceRepository, MatrixReferenceRepository>();
        services.AddScoped<ISchoolDirectoryRepository, SchoolDirectoryRepository>();
        services.AddScoped<ISchoolDirectoryAdminRepository, SchoolDirectoryAdminRepository>();
        services.AddScoped<IStudentImportRepository, StudentImportRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IExamSubjectRepository, ExamSubjectRepository>();

        services.AddSingleton<IProvinceProvider>(_ => CreateProvinceProvider(configuration));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMatrixRoleCatalog, ConfiguredMatrixRoleCatalog>();
        services.AddSingleton<IMatrixWorkbookExporter, ClosedXmlMatrixWorkbookExporter>();

        return services;
    }

    private static IProvinceProvider CreateProvinceProvider(IConfiguration configuration)
    {
        const string allowedHost = "danhmuchanhchinh.nso.gov.vn";
        var baseUrl = configuration["ProvinceProvider:BaseUrl"]
            ?? $"https://{allowedHost}/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(baseUri.Host, allowedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ProvinceProvider:BaseUrl must use HTTPS and host '{allowedHost}'.");
        }

        var configuredTimeout = configuration["ProvinceProvider:TimeoutSeconds"];
        var timeoutSeconds = string.IsNullOrWhiteSpace(configuredTimeout)
            ? 15
            : int.TryParse(configuredTimeout, out var parsedTimeout)
                ? parsedTimeout
                : 0;
        if (timeoutSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "ProvinceProvider:TimeoutSeconds must be between 1 and 60.");
        }

        return new NsoProvinceProvider(new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        });
    }
}
