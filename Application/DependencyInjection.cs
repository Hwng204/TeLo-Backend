using Application.Services.Implement;
using Application.Services.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<ICurriculumService, CurriculumService>();
        services.AddScoped<IMatrixApplicationService, MatrixApplicationService>();
        services.AddScoped<IMatrixImportService, MatrixImportService>();
        services.AddScoped<IMatrixTaskApplicationService, MatrixTaskApplicationService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<ISchoolBranchService, SchoolBranchService>();
        services.AddScoped<IMatrixPeopleResolver, MatrixPeopleResolver>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailNotificationService, LockedEmailNotificationService>();

        services.AddScoped<TaskJournal>();
        services.AddScoped<QuestionAccess>();
        services.AddScoped<RequiredRolesService>();
        services.AddScoped<ExamPackageStorage>();
        services.AddScoped<QuestionService>();
        services.AddScoped<QuestionBankService>();
        services.AddScoped<QuestionImportService>();
        services.AddScoped<QuestionPaperService>();
        services.AddScoped<ExamPackageService>();
        return services;
    }
}
