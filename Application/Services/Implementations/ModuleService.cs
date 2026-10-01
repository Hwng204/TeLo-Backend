using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using static Application.Common.IdentityManagementRules;

namespace Application.Services.Implement;

public sealed class ModuleService(ApplicationDbContext db) : IModuleService
{
    private static readonly Expression<Func<Module, ModuleItem>> Projection = m => new ModuleItem(
        m.Id, m.Code, m.Name, m.Description, m.Status, m.Version, m.Navbars.Count, !m.Navbars.Any());

    public async Task<ServiceResult<DirectoryPage<ModuleItem>>> ListAsync(IdentityListQuery query, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<ModuleItem>>(errors);
        var rows = db.Modules.AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(m => m.Code.Contains(search) || m.Name.Contains(search));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(m => m.Status == query.Status);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(m => m.Name).ThenBy(m => m.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(Projection).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<ModuleItem>>.Success(new(items, query.Page, query.PageSize, count));
    }

    public async Task<ServiceResult<ModuleItem>> GetAsync(ulong id, CancellationToken ct)
    {
        var item = await db.Modules.AsNoTracking().Where(m => m.Id == id).Select(Projection).SingleOrDefaultAsync(ct);
        return item == null ? Missing<ModuleItem>() : ServiceResult<ModuleItem>.Success(item);
    }

    public async Task<ServiceResult<ModuleItem>> SaveAsync(ulong? id, SaveModuleRequest request, ulong actor, CancellationToken ct)
    {
        var errors = IdentityManagementValidator.Module(request);
        if (errors.Count > 0) return Invalid<ModuleItem>(errors);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<ModuleItem>();
        var module = id.HasValue ? await db.Modules.SingleOrDefaultAsync(m => m.Id == id, ct) : new Module();
        if (module == null) return Missing<ModuleItem>();
        if (id.HasValue && (request.Version == 0 || request.Version != module.Version)) return Conflict<ModuleItem>();
        if (id.HasValue && !string.Equals(module.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            return Invalid<ModuleItem>(new Dictionary<string, string[]> { ["code"] = ["Không được thay đổi mã module."] });
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        if (await db.Modules.AnyAsync(m => (m.Code == code || m.Name == name) && (!id.HasValue || m.Id != id.Value), ct))
            return Conflict<ModuleItem>("Mã hoặc tên module đã tồn tại.");
        var before = Snapshot(module);
        if (!id.HasValue) { module.Code = code; db.Modules.Add(module); }
        else module.Version++;
        module.Name = name;
        module.Description = request.Description?.Trim();
        await db.SaveChangesAsync(ct);
        Audit(actor, module.Id, id.HasValue ? "UPDATE" : "CREATE", new { Before = id.HasValue ? before : null, After = Snapshot(module) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(module.Id, ct);
    }

    public async Task<ServiceResult<ModuleItem>> StatusAsync(ulong id, IdentityStatusRequest request, ulong actor, CancellationToken ct)
    {
        if (request.Status is not ("ACTIVE" or "INACTIVE")) return Invalid<ModuleItem>(new Dictionary<string, string[]> { ["status"] = ["Trạng thái không hợp lệ."] });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<ModuleItem>();
        var module = await db.Modules.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (module == null) return Missing<ModuleItem>();
        if (request.Version == 0 || request.Version != module.Version) return Conflict<ModuleItem>();
        if (module.Status != request.Status)
        {
            var before = Snapshot(module);
            module.Status = request.Status;
            module.Version++;
            Audit(actor, id, "STATUS", new { Before = before, After = Snapshot(module) });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(ulong id, uint version, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeMutationAsync(db, actor, ct)) return Forbidden<bool>();
        var module = await db.Modules.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (module == null) return Missing<bool>();
        if (version == 0 || version != module.Version) return Conflict<bool>();
        if (await db.Navbars.AnyAsync(n => n.ModuleId == id, ct)) return Conflict<bool>("Module đang có mục điều hướng. Hãy ngừng áp dụng để giữ dữ liệu liên quan.");
        Audit(actor, id, "DELETE", Snapshot(module));
        db.Modules.Remove(module);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private static object Snapshot(Module module) => new { module.Code, module.Name, module.Description, module.Status, module.Version };
    private void Audit(ulong actor, ulong id, string action, object data) => db.IdentityAudits.Add(new IdentityAudit
    { ActorUserId = actor, EntityType = "MODULE", EntityId = id, Action = action, Data = JsonSerializer.Serialize(data) });
}
