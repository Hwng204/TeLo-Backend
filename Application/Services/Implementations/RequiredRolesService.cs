using Application.Common;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Implement;

public sealed record RequiredRoleDefinition(string Code, string Name, string[] Aliases);
public sealed record RequiredRoleStatus(string Code, string Name, bool Exists);

public sealed class RequiredRolesService(ApplicationDbContext db)
{
    public static readonly IReadOnlyList<RequiredRoleDefinition> Definitions = new RequiredRoleDefinition[]
    {
        new("ADMIN", "Admin", ["ADMIN", "OPERATIONALADMIN"]),
        new("TEACHER", "Giáo viên", ["TEACHER", "GIAO_VIEN"]),
        new("TEAM_LEAD", "Tổ trưởng", ["TEAM_LEAD", "TO_TRUONG", "SUBJECT_COORDINATOR"]),
        new("HIEU_TRUONG", "Hiệu trưởng", ["HIEU_TRUONG", "PRINCIPAL"]),
        new("STUDENT", "Học sinh", ["STUDENT", "HOC_SINH"]),
        new("PHT", "Phó Hiệu trưởng", ["PHT", "PHO_HIEU_TRUONG", "VICE_PRINCIPAL"])
    };

    public static RequiredRoleStatus[] MissingFrom(IEnumerable<string> codes)
    {
        var existing = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Definitions.Select(definition => new RequiredRoleStatus(definition.Code, definition.Name,
            definition.Aliases.Any(existing.Contains))).ToArray();
    }

    public async Task<RequiredRoleStatus[]> GetAsync(CancellationToken ct) =>
        MissingFrom(await db.Roles.AsNoTracking().Select(role => role.Code).ToArrayAsync(ct));

    // Explicit admin action only. Never called by startup, login, GET, or tests
    // against the shared database. Existing aliases/IDs/grants are preserved.
    public async Task<ServiceResult<RequiredRoleStatus[]>> RestoreAsync(ulong actorId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        if (!await IdentityManagementRules.AuthorizeMutationAsync(db, actorId, ct))
            return IdentityManagementRules.Forbidden<RequiredRoleStatus[]>();
        await db.Database.SqlQueryRaw<ulong>("SELECT role_id AS Value FROM role ORDER BY role_id LIMIT 1 FOR UPDATE").ToArrayAsync(ct);
        var current = await GetAsync(ct);
        foreach (var item in current.Where(item => !item.Exists))
            db.Roles.Add(new Role { Code = item.Code, Name = item.Name, Status = "ACTIVE" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<RequiredRoleStatus[]>.Success(await GetAsync(ct));
    }
}
