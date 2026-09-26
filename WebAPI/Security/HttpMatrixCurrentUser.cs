using System.Security.Claims;
using Application.Common;
using Application.Common.Security;
using Domain.Entities.QuestionBank;
using Infrastructure.Security;

namespace WebAPI.Security;

public sealed class HttpMatrixCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    IMatrixRoleCatalog roleCatalog) : IMatrixCurrentUser
{
    public MatrixActor Actor
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("Cần đăng nhập để thực hiện thao tác này.");
            }

            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                principal.FindFirstValue("sub");
            if (!ulong.TryParse(userIdValue, out var userId) || userId == 0)
            {
                throw new UnauthorizedAccessException("Tài khoản không hợp lệ.");
            }

            var roleCodes = principal.Claims
                .Where(claim =>
                    claim.Type == ClaimTypes.Role ||
                    claim.Type == "role" ||
                    claim.Type == "roles")
                .Select(claim => claim.Value)
                .ToArray();

            if (roleCodes.Any(roleCatalog.IsPrincipal))
            {
                return new MatrixActor(userId, MatrixActorRole.Pht, null, true);
            }

            if (roleCodes.Any(roleCatalog.IsPht))
            {
                if (!ulong.TryParse(
                        principal.FindFirstValue(MatrixClaims.BranchId),
                        out var branchId) ||
                    branchId == 0)
                {
                    throw new MatrixApplicationException(
                        "Forbidden",
                        "Tài khoản PHT chưa được gán chi nhánh.");
                }

                return new MatrixActor(userId, MatrixActorRole.Pht, branchId);
            }

            if (roleCodes.Any(roleCatalog.IsTeamLead))
            {
                return new MatrixActor(userId, MatrixActorRole.TeamLead);
            }

            throw new MatrixApplicationException(
                "Forbidden",
                "Tài khoản không có vai trò được phép thao tác ma trận.");
        }
    }
}
