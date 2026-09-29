using System.Security.Claims;
using Application.Common;
using Application.Services.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace WebAPI.Controllers;

[ApiController]
public abstract class SchoolDirectoryControllerBase : ControllerBase
{
    public const string SchoolReadPolicy = "SchoolDirectorySchoolRead";
    public const string AdminPolicy = "SchoolDirectoryAdmin";

    // Narrower than SchoolReadPolicy: teachers and team leads may read but never import.
    public const string ImportPolicy = "SchoolDirectorySchoolImport";

    protected ulong ActorUserId =>
        ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // School routes never accept a schoolId: the scope comes from the signed-in user's branch.
    protected DirectoryScope ActorScope => DirectoryScope.FromActor(ActorUserId, IsHomeroomOnly());

    // A user whose every role is a teacher role only sees the class they lead. Anyone who also holds
    // a school-wide role (principal, vice principal, team lead) keeps the whole-school view.
    private bool IsHomeroomOnly()
    {
        var homeroomRoles = HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("SchoolDirectoryAuth:HomeroomOnlyRoleCodes").Get<string[]>()
            ?? ["GIAO_VIEN", "TEACHER"];
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        return roles.Length > 0 && roles.All(role => homeroomRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

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
            SchoolDirectoryErrorCodes.ImportFileInvalid or
            SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound =>
                UnprocessableEntity(response),
            SchoolDirectoryErrorCodes.SchoolScopeRequired =>
                StatusCode(StatusCodes.Status403Forbidden, response),
            SchoolDirectoryErrorCodes.StudentNotFound or
            SchoolDirectoryErrorCodes.ClassNotFound or
            SchoolDirectoryErrorCodes.ImportBatchNotFound or
            SchoolDirectoryErrorCodes.SchoolNotFound =>
                NotFound(response),
            SchoolDirectoryErrorCodes.StudentCodeDuplicate or
            SchoolDirectoryErrorCodes.ClassCodeDuplicate or
            SchoolDirectoryErrorCodes.TeacherAlreadyHomeroom or
            SchoolDirectoryErrorCodes.ClassHasActiveStudents or
            SchoolDirectoryErrorCodes.ImportBatchStateInvalid or
            SchoolDirectoryErrorCodes.ImportHasInvalidRows =>
                Conflict(response),
            SchoolDirectoryErrorCodes.Unauthorized => Unauthorized(response),
            _ => StatusCode(StatusCodes.Status500InternalServerError, response)
        };
    }
}
