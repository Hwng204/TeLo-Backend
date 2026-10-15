using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

public abstract class QuestionControllerBase(IMatrixRoleCatalog roles, IConfiguration configuration) : ControllerBase
{
    protected QuestionActor Actor()
    {
        var codes = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        return new(ulong.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0,
            codes.Intersect(TaskService.TeacherRoleCodes(configuration), StringComparer.OrdinalIgnoreCase).Any(), codes.Any(roles.IsTeamLead), codes.Any(roles.IsPht), codes.Any(roles.IsPrincipal));
    }

    protected async Task<IActionResult> Result<T>(Func<Task<T>> action, string? message = null)
    {
        try { return Ok(ApiResponse<T>.Ok(await action(), message)); }
        catch (QuestionException ex) { return StatusCode(ex.Status, ApiResponse<T>.Fail(ex.Code, ex.Message, ex.Details)); }
    }
}
