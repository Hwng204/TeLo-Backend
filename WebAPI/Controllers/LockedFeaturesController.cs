using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

// Khoá tạm DB v2: các module này chưa chuyển sang bảng của ERD; code cũ vẫn trong repo nhưng bị loại khỏi
// biên dịch (xem <Compile Remove> trong các .csproj và DB_V2.md mục 4). Chủ module viết lại xong
// thì bỏ route tương ứng ở đây.
[ApiController]
[Authorize]
[Route("api/exams/{**rest}")]
[Route("api/schools/{schoolId:long}/emails/{**rest}")]
[Route("api/provinces/{**rest}")]
[Route("api/student-imports/{**rest}")]
[Route("api/admin/schools/{schoolId:long}/student-imports/{**rest}")]
public sealed class LockedFeaturesController : ControllerBase
{
    public const string ErrorCode = "FEATURE_LOCKED_DB_V2";

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    public IActionResult Locked() => StatusCode(StatusCodes.Status501NotImplemented, ApiResponse<object>.Fail(
        ErrorCode, "Chức năng đang được chuyển sang cơ sở dữ liệu mới nên tạm thời chưa dùng được."));
}
