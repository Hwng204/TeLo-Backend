using Application;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using WebAPI.ExceptionHandling;
using WebAPI.Controllers;
using WebAPI.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
            Type = "https://httpstatuses.com/400"
        };
        problem.Extensions["code"] = "ValidationError";
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddMatrixIdentity();
builder.Services.AddAuthorization(options =>
{
    // Role codes live in config so a school can rename them without touching the controllers.
    var schoolReadRoles = builder.Configuration
        .GetSection("SchoolDirectoryAuth:SchoolReadRoleCodes").Get<string[]>()
        ?? ["HIEU_TRUONG", "PRINCIPAL", "PHT", "GIAO_VIEN", "TEACHER", "TEAM_LEAD", "TO_TRUONG"];
    var directoryAdminRoles = builder.Configuration
        .GetSection("SchoolDirectoryAuth:AdminRoleCodes").Get<string[]>()
        ?? ["OperationalAdmin"];
    options.AddPolicy(SchoolDirectoryControllerBase.SchoolReadPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireRole(schoolReadRoles));
    options.AddPolicy(SchoolDirectoryControllerBase.AdminPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireRole(directoryAdminRoles));

    var teacherReadRoles = new[] { "MatrixAuth:PrincipalRoleCodes", "MatrixAuth:PhtRoleCodes" }
        .SelectMany(key => builder.Configuration.GetSection(key).Get<string[]>() ??
            (key.Contains("Principal") ? ["HIEU_TRUONG", "PRINCIPAL"] : new[] { "PHT" })).ToArray();
    options.AddPolicy("TeacherRead", policy => policy.RequireAuthenticatedUser().RequireRole(teacherReadRoles));

    options.AddPolicy("OperationalAdmin", policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context =>
            context.User.IsInRole("OperationalAdmin") ||
            context.User.HasClaim("permission", "academic_calendar.manage")));
});
builder.Services.AddProblemDetails();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins is { Length: > 0 }
        ? allowedOrigins
        : ["http://localhost:5173", "http://localhost:3000"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));
builder.Services.AddExceptionHandler<MatrixExceptionHandler>();
builder.Services.AddExceptionHandler<TeacherExceptionHandler>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
