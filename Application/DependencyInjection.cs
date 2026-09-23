using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<IProvinceCatalogService, ProvinceCatalogService>();
        services.AddScoped<IProvinceSyncService, ProvinceSyncService>();
        services.AddScoped<ISchoolDirectoryService, SchoolDirectoryService>();
        services.AddScoped<ISchoolDirectoryAdminService, SchoolDirectoryAdminService>();
        services.AddScoped<IStudentImportService, StudentImportService>();
        services.AddScoped<IMatrixApplicationService,MatrixApplicationService>();
        services.AddScoped<IMatrixTaskApplicationService, MatrixTaskApplicationService>();
        services.AddScoped<IMatrixPeopleResolver, MatrixPeopleResolver>();

        return services;
    }
}
