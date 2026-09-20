using System.Security.Claims;
using Application.Common;
using Application.Services.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
public abstract class SchoolDirectoryControllerBase : ControllerBase
{
    public const string SchoolReadPolicy = "SchoolDirectorySchoolRead";
    public const string AdminPolicy = "SchoolDirectoryAdmin";

    protected ulong ActorUserId =>
        ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // School routes never accept a schoolId: the scope comes from the signed-in user's branch.
    protected DirectoryScope ActorScope => DirectoryScope.FromActor(ActorUserId);

    protected IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.Error is not { } error)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!));
        }

        var response = ApiResponse<T>.Fail(error.Code, error.Message, error.Details);
        return error.Code switch
        {
            SchoolDirectoryErrorCodes.Validation or
            SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound =>
                UnprocessableEntity(response),
            SchoolDirectoryErrorCodes.SchoolScopeRequired =>
                StatusCode(StatusCodes.Status403Forbidden, response),
            SchoolDirectoryErrorCodes.StudentNotFound or
            SchoolDirectoryErrorCodes.ClassNotFound or
            SchoolDirectoryErrorCodes.SchoolNotFound =>
                NotFound(response),
            SchoolDirectoryErrorCodes.StudentCodeDuplicate or
            SchoolDirectoryErrorCodes.ClassCodeDuplicate or
            SchoolDirectoryErrorCodes.TeacherAlreadyHomeroom or
            SchoolDirectoryErrorCodes.ClassHasActiveStudents =>
                Conflict(response),
            SchoolDirectoryErrorCodes.Unauthorized => Unauthorized(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
