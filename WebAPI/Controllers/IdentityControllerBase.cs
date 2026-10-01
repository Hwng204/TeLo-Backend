using System.Security.Claims;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "IdentityAdmin")]
public abstract class IdentityControllerBase : ControllerBase
{
    protected ulong ActorId => ulong.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected async Task<IActionResult> Respond<T>(Func<Task<ServiceResult<T>>> action, bool created = false)
    {
        try
        {
            var result = await action();
            if (result.IsSuccess) return StatusCode(created ? 201 : 200, ApiResponse<T>.Ok(result.Value!));
            var error = result.Error!;
            var status = error.Code switch { "VALIDATION_ERROR" => 422, "NOT_FOUND" => 404, "FORBIDDEN" => 403, _ => 409 };
            return StatusCode(status, ApiResponse<T>.Fail(error.Code, error.Message, error.Details));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(ApiResponse<T>.Fail("STALE_VERSION", "Dữ liệu đã thay đổi. Vui lòng tải lại."));
        }
        catch (Exception ex) when (IsWriteConflict(ex))
        {
            var mysql = ex as MySqlException ?? ex.InnerException as MySqlException;
            return Conflict(ApiResponse<T>.Fail(mysql?.Number is 1205 or 1213 ? "STALE_VERSION" : "CONFLICT", "Dữ liệu trùng hoặc vừa được thay đổi. Vui lòng tải lại và thử lại."));
        }
    }

    private static bool IsWriteConflict(Exception error) => error is MySqlException { Number: 1062 or 1205 or 1213 or 1451 or 1452 }
        || error.InnerException is MySqlException { Number: 1062 or 1205 or 1213 or 1451 or 1452 };
}
