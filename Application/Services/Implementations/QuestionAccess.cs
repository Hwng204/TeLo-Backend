using Application.Common;
using Application.DTOs;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services.Implement;

public sealed class QuestionAccess(ApplicationDbContext db, IMatrixRoleCatalog roles, IConfiguration configuration)
{
    public async Task<User> UserAsync(QuestionActor actor, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(x => x.SchoolBranch).ThenInclude(x => x!.School)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Id == actor.UserId && x.Status == "ACTIVE", ct);
        if (user is null || user.SchoolBranch is { } branch && (branch.Status != "ACTIVE" || branch.School.Status != "ACTIVE"))
            throw new QuestionException("FORBIDDEN", "Tài khoản hoặc phạm vi làm việc không còn hoạt động.", 403);
        var codes = user.UserRoles.Where(grant => grant.Role.Status == "ACTIVE").Select(grant => grant.Role.Code).ToArray();
        if (actor.Teacher && !codes.Intersect(TaskService.TeacherRoleCodes(configuration), StringComparer.OrdinalIgnoreCase).Any() ||
            actor.TeamLead && !codes.Any(roles.IsTeamLead) || actor.Pht && !codes.Any(roles.IsPht) || actor.Principal && !codes.Any(roles.IsPrincipal) ||
            !actor.Teacher && !actor.TeamLead && !actor.Pht && !actor.Principal)
            throw new QuestionException("FORBIDDEN", "Quyền đã thay đổi. Vui lòng đăng nhập lại.", 403);
        return user;
    }
}
