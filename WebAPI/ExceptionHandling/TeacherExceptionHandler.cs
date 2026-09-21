using Application.Common;
using Domain.Entities.Identity;
using Microsoft.AspNetCore.Diagnostics;

namespace WebAPI.ExceptionHandling;

public sealed class TeacherExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not TeacherManagementException error) return false;
        context.Response.StatusCode = error.Code switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status422UnprocessableEntity,
            "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
            "FORBIDDEN" or "PROTECTED_ACCOUNT" => StatusCodes.Status403Forbidden,
            "SCHOOL_NOT_FOUND" or "BRANCH_NOT_FOUND" or "TEACHER_NOT_FOUND" => StatusCodes.Status404NotFound,
            "TEACHER_ROLE_MISSING" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status409Conflict
        };
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(error.Code, error.Message), ct);
        return true;
    }
}
