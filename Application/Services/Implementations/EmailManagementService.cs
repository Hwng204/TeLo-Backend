using System.Data;
using System.Linq.Expressions;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.Notification;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static Application.Common.IdentityManagementRules;

namespace Application.Services.Implement;

public sealed class EmailManagementService(
    ApplicationDbContext db,
    IMatrixRoleCatalog roleCatalog,
    EmailRecipientResolver recipients,
    IEmailNotificationService notifications,
    IConfiguration configuration) : IEmailManagementService
{
    private static readonly Expression<Func<EmailTemplate, EmailTemplateItem>> TemplateProjection = t => new(
        t.Id, t.Code, t.Name, t.EventCode, t.Status, t.Version,
        t.Revisions.OrderByDescending(r => r.Revision).Select(r => r.Id).FirstOrDefault(), t.UsedAt == null);

    private static readonly Expression<Func<EmailTemplateVersion, EmailRevisionItem>> RevisionProjection = r =>
        new(r.Id, r.Revision, r.Subject, r.Body, r.CreatedAt, r.VariablesJson, r.EventVersion);

    private static readonly Expression<Func<Notification, EmailHistoryItem>> HistoryProjection = n => new(
        n.Id, n.Title, n.EventCode, n.CreatedAt, n.Recipients.Count,
        n.Recipients.Count(r => r.EmailStatus == "SENT"),
        n.Recipients.Count(r => r.EmailStatus == "PENDING" || r.EmailStatus == "SENDING"),
        n.Recipients.Count(r => r.EmailStatus == "ERROR"),
        n.Recipients.Count(r => r.EmailStatus == "CANCELLED"), n.IsTest, n.ScheduledFor, n.SendKind, n.Version,
        n.SendKind == "MANUAL" && n.CancelledAt == null && n.Recipients.Any() && !n.Recipients.Any(r => r.AttemptCount > 0 || r.EmailStatus == "SENDING" || r.EmailStatus == "SENT"));

    public async Task<ServiceResult<DirectoryPage<EmailSchoolItem>>> SchoolsAsync(EmailListQuery query, ulong actor, CancellationToken ct)
    {
        var errors = EmailTemplateRules.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailSchoolItem>>(errors);
        var access = await AccessAsync(actor, ct);
        if (!access.GlobalAdmin && access.SchoolId == null) return Denied<DirectoryPage<EmailSchoolItem>>();
        var rows = db.Schools.AsNoTracking().Where(s => s.Status == "ACTIVE" && (access.GlobalAdmin || s.Id == access.SchoolId));
        if (query.SchoolId.HasValue) rows = rows.Where(s => s.Id == query.SchoolId);
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(s => s.Code.Contains(search) || s.Name.Contains(search));
        return await PageAsync(rows.OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new EmailSchoolItem(s.Id, s.Code, s.Name, access.GlobalAdmin || access.SchoolAdmin)), query, ct);
    }

    private IQueryable<EmailEvent> EventRows(ulong schoolId) => db.EmailEvents.Where(e => e.SchoolId == null || e.SchoolId == schoolId);

    public async Task<ServiceResult<DirectoryPage<EmailEventItem>>> EventsAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailEventItem>>();
        var errors = ListErrors(query, false);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailEventItem>>(errors);
        var rows = EventRows(schoolId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(e => e.Name.Contains(query.Search.Trim()) || e.Code.Contains(query.Search.Trim()));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(e => e.Status == query.Status);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(e => e.TriggerKind).ThenBy(e => e.Name).ThenBy(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<EmailEventItem>>.Success(new(items.Select(EmailTemplateRules.Definition).ToArray(), query.Page, query.PageSize, count));
    }

    public async Task<ServiceResult<EmailEventItem>> EventAsync(ulong schoolId, string code, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<EmailEventItem>();
        var row = await EventRows(schoolId).AsNoTracking().SingleOrDefaultAsync(e => e.Code == code, ct);
        return row == null ? Missing<EmailEventItem>() : ServiceResult<EmailEventItem>.Success(EmailTemplateRules.Definition(row));
    }

    public async Task<ServiceResult<EmailEventItem>> SaveEventAsync(ulong schoolId, string? code, SaveEmailEventRequest request, ulong actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<EmailEventItem>();
        var errors = EmailTemplateRules.ValidateEvent(request);
        if (errors.Count > 0) return Invalid<EmailEventItem>(errors);
        var normalized = request.Code.Trim().ToUpperInvariant();
        var row = code == null ? new EmailEvent { SchoolId = schoolId, Code = normalized } : await EventRows(schoolId).SingleOrDefaultAsync(e => e.Code == code, ct);
        if (row == null) return Missing<EmailEventItem>();
        if (row.TriggerKind != "MANUAL" || row.SchoolId != schoolId) return Denied<EmailEventItem>();
        if (code != null && (request.Version == 0 || row.Version != request.Version)) return Conflict<EmailEventItem>();
        if (code != null && row.Code != normalized) return FieldError<EmailEventItem>("code", "Không được đổi mã sự kiện đã tạo.");
        if (code == null && await EventRows(schoolId).AnyAsync(e => e.Code == normalized, ct)) return Conflict<EmailEventItem>("Mã sự kiện đã tồn tại trong trường hoặc thuộc sự kiện hệ thống.");
        if (code == null) db.EmailEvents.Add(row); else row.Version++;
        row.Name = request.Name.Trim(); row.Description = request.Description.Trim();
        row.VariablesJson = JsonSerializer.Serialize(EmailTemplateRules.CommonVariables.Concat(request.VariableDefinitions));
        await db.SaveChangesAsync(ct);
        Audit(actor, "EMAIL_EVENT", row.Id, code == null ? "CREATE" : "UPDATE", new { row.SchoolId, row.Code, row.Name, row.Version, row.VariablesJson });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<EmailEventItem>.Success(EmailTemplateRules.Definition(row));
    }

    public async Task<ServiceResult<EmailEventItem>> EventStatusAsync(ulong schoolId, string code, EmailStatusRequest request, ulong actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<EmailEventItem>();
        var row = await db.EmailEvents.SingleOrDefaultAsync(e => e.SchoolId == schoolId && e.Code == code, ct);
        if (row == null) return Missing<EmailEventItem>();
        if (request.Status is not ("ACTIVE" or "INACTIVE")) return FieldError<EmailEventItem>("status", "Trạng thái không hợp lệ.");
        if (request.Version == 0 || row.Version != request.Version) return Conflict<EmailEventItem>();
        row.Status = request.Status; row.Version++;
        Audit(actor, "EMAIL_EVENT", row.Id, "STATUS", new { row.Status, row.Version });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<EmailEventItem>.Success(EmailTemplateRules.Definition(row));
    }

    public async Task<ServiceResult<bool>> DeleteEventAsync(ulong schoolId, string code, uint version, ulong actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<bool>();
        var row = await db.EmailEvents.SingleOrDefaultAsync(e => e.SchoolId == schoolId && e.Code == code, ct);
        if (row == null) return Missing<bool>();
        if (version == 0 || row.Version != version) return Conflict<bool>();
        if (row.UsedAt != null || await db.EmailTemplates.AnyAsync(t => t.SchoolId == schoolId && t.EventCode == code, ct)
            || await db.NotificationConfigs.AnyAsync(c => c.SchoolId == schoolId && c.EventCode == code, ct))
            return Conflict<bool>("Sự kiện đã được sử dụng. Hãy ngừng áp dụng để giữ lịch sử.");
        Audit(actor, "EMAIL_EVENT", row.Id, "DELETE", new { row.Code, row.SchoolId });
        db.EmailEvents.Remove(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<EmailRuntimeStatus>> RuntimeAsync(ulong schoolId, ulong actor, CancellationToken ct) =>
        await AllowedAsync(schoolId, actor, false, ct) ? ServiceResult<EmailRuntimeStatus>.Success(new(
            bool.TryParse(configuration["NotificationEmail:Enabled"], out var enabled) && enabled,
            !string.IsNullOrWhiteSpace(configuration["EmailSettings:Email"]) && !string.IsNullOrWhiteSpace(configuration["EmailSettings:Password"]))) : Denied<EmailRuntimeStatus>();

    public async Task<ServiceResult<EmailMessagePreview>> MessagePreviewAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct) =>
        await AllowedAsync(schoolId, actor, false, ct) ? await notifications.PreviewManualAsync(schoolId, request, actor, ct) : Denied<EmailMessagePreview>();

    public async Task<ServiceResult<EmailQueueResult>> SendAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, false, ct)) return Denied<EmailQueueResult>();
        var result = await notifications.QueueManualAsync(schoolId, request, actor, ct);
        if (!result.IsSuccess) return result;
        Audit(actor, "EMAIL_NOTIFICATION", result.Value!.NotificationId, "SEND", new { request.EventCode, request.ScheduledFor });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<ServiceResult<bool>> CancelAsync(ulong schoolId, ulong id, uint version, ulong actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, false, ct)) return Denied<bool>();
        var row = (await db.Notifications.FromSqlInterpolated($"SELECT * FROM notifications WHERE id = {id} AND school_id = {schoolId} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault();
        if (row == null) return Missing<bool>();
        if (version == 0 || row.Version != version) return Conflict<bool>();
        if (row.SendKind != "MANUAL" || row.CancelledAt != null || await db.NotificationRecipients.AnyAsync(r => r.NotificationId == id && (r.AttemptCount > 0 || r.EmailStatus == "SENDING" || r.EmailStatus == "SENT"), ct))
            return Conflict<bool>("Lần gửi đã bắt đầu hoặc đã được hủy. Vui lòng tải lại lịch sử.");
        row.CancelledAt = DateTime.UtcNow; row.Version++;
        await db.NotificationRecipients.Where(r => r.NotificationId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.EmailStatus, "CANCELLED").SetProperty(r => r.LastError, "CANCELLED_BY_MANAGER"), ct);
        Audit(actor, "EMAIL_NOTIFICATION", id, "CANCEL", new { row.SchoolId, row.Version });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<DirectoryPage<EmailTemplateItem>>> TemplatesAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailTemplateItem>>();
        var errors = ListErrors(query, false);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailTemplateItem>>(errors);
        var rows = db.EmailTemplates.AsNoTracking().Where(t => t.SchoolId == schoolId);
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(t => t.Code.Contains(search) || t.Name.Contains(search));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(t => t.Status == query.Status);
        if (!string.IsNullOrEmpty(query.EventCode)) rows = rows.Where(t => t.EventCode == query.EventCode);
        return await PageAsync(rows.OrderBy(t => t.Name).ThenBy(t => t.Id).Select(TemplateProjection), query, ct);
    }

    public async Task<ServiceResult<EmailTemplateDetail>> TemplateAsync(ulong schoolId, ulong id, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<EmailTemplateDetail>();
        return await ReadTemplateAsync(schoolId, id, ct);
    }

    public async Task<ServiceResult<EmailTemplateDetail>> SaveTemplateAsync(ulong schoolId, ulong? id, SaveEmailTemplateRequest request, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<EmailTemplateDetail>();
        var eventRow = await EventRows(schoolId).SingleOrDefaultAsync(e => e.Code == request.EventCode, ct);
        if (eventRow == null || eventRow.Status != "ACTIVE") return FieldError<EmailTemplateDetail>("eventCode", "Chọn sự kiện đang áp dụng trong trường.");
        var errors = EmailTemplateRules.ValidateTemplate(request, EmailTemplateRules.Definition(eventRow));
        if (errors.Count > 0) return Invalid<EmailTemplateDetail>(errors);
        var template = id.HasValue
            ? await db.EmailTemplates.SingleOrDefaultAsync(t => t.SchoolId == schoolId && t.Id == id, ct)
            : new EmailTemplate { SchoolId = schoolId };
        if (template == null) return Missing<EmailTemplateDetail>();
        if (id.HasValue && (request.Version == 0 || request.Version != template.Version)) return Conflict<EmailTemplateDetail>();
        var code = request.Code.Trim().ToUpperInvariant();
        var eventCode = request.EventCode.Trim().ToUpperInvariant();
        if (id.HasValue && (template.Code != code || template.EventCode != eventCode))
            return FieldError<EmailTemplateDetail>("code", "Không được đổi mã mẫu hoặc sự kiện của mẫu đã tạo.");
        if (await db.EmailTemplates.AnyAsync(t => t.SchoolId == schoolId && t.Code == code && (!id.HasValue || t.Id != id.Value), ct))
            return Conflict<EmailTemplateDetail>("Mã mẫu email đã tồn tại trong trường.");
        var before = TemplateSnapshot(template);
        uint revision = id.HasValue
            ? (await db.EmailTemplateVersions.Where(r => r.EmailTemplateId == template.Id).MaxAsync(r => (uint?)r.Revision, ct) ?? 0) + 1
            : 1;
        if (!id.HasValue)
        {
            template.Code = code;
            template.EventCode = eventCode;
            db.EmailTemplates.Add(template);
        }
        else template.Version++;
        template.Name = request.Name.Trim();
        var newRevision = new EmailTemplateVersion
        {
            Template = template, Revision = revision, Subject = request.Subject.Trim(),
            Body = request.Body.Trim(), CreatedByUserId = actor, VariablesJson = eventRow.VariablesJson, EventVersion = eventRow.Version
        };
        eventRow.UsedAt ??= DateTime.UtcNow;
        db.EmailTemplateVersions.Add(newRevision);
        await db.SaveChangesAsync(ct);
        Audit(actor, "EMAIL_TEMPLATE", template.Id, id.HasValue ? "UPDATE" : "CREATE",
            new { Before = id.HasValue ? before : null, After = TemplateSnapshot(template), RevisionId = newRevision.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ReadTemplateAsync(schoolId, template.Id, ct);
    }

    public async Task<ServiceResult<DirectoryPage<EmailRevisionItem>>> RevisionsAsync(ulong schoolId, ulong id, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailRevisionItem>>();
        var errors = EmailTemplateRules.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailRevisionItem>>(errors);
        if (!await db.EmailTemplates.AnyAsync(t => t.SchoolId == schoolId && t.Id == id, ct)) return Missing<DirectoryPage<EmailRevisionItem>>();
        return await PageAsync(db.EmailTemplateVersions.AsNoTracking()
            .Where(r => r.Template.SchoolId == schoolId && r.EmailTemplateId == id)
            .OrderByDescending(r => r.Revision).Select(RevisionProjection), query, ct);
    }

    public async Task<ServiceResult<EmailTemplateDetail>> TemplateStatusAsync(ulong schoolId, ulong id, EmailStatusRequest request, ulong actor, CancellationToken ct)
    {
        if (request.Status is not ("ACTIVE" or "INACTIVE")) return FieldError<EmailTemplateDetail>("status", "Trạng thái không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<EmailTemplateDetail>();
        var template = await db.EmailTemplates.SingleOrDefaultAsync(t => t.SchoolId == schoolId && t.Id == id, ct);
        if (template == null) return Missing<EmailTemplateDetail>();
        if (request.Version == 0 || request.Version != template.Version) return Conflict<EmailTemplateDetail>();
        if (template.Status != request.Status)
        {
            var before = TemplateSnapshot(template);
            template.Status = request.Status;
            template.Version++;
            Audit(actor, "EMAIL_TEMPLATE", id, "STATUS", new { Before = before, After = TemplateSnapshot(template) });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return await ReadTemplateAsync(schoolId, id, ct);
    }

    public async Task<ServiceResult<bool>> DeleteTemplateAsync(ulong schoolId, ulong id, uint version, ulong actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, true, ct)) return Denied<bool>();
        var template = await db.EmailTemplates.SingleOrDefaultAsync(t => t.SchoolId == schoolId && t.Id == id, ct);
        if (template == null) return Missing<bool>();
        if (version == 0 || template.Version != version) return Conflict<bool>();
        if (template.UsedAt != null || await db.NotificationConfigs.AnyAsync(c => c.EmailTemplateVersion != null && c.EmailTemplateVersion.EmailTemplateId == id, ct)
            || await db.Notifications.AnyAsync(n => n.EmailTemplateVersion != null && n.EmailTemplateVersion.EmailTemplateId == id, ct))
            return Conflict<bool>("Mẫu đã được sử dụng. Hãy ngừng áp dụng để giữ lịch sử.");
        Audit(actor, "EMAIL_TEMPLATE", id, "DELETE", TemplateSnapshot(template));
        db.EmailTemplateVersions.RemoveRange(await db.EmailTemplateVersions.Where(r => r.EmailTemplateId == id).ToArrayAsync(ct));
        db.EmailTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<DirectoryPage<EmailConfigurationListItem>>> ConfigurationsAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailConfigurationListItem>>();
        var errors = EmailTemplateRules.Query(query);
        if (!string.IsNullOrEmpty(query.ConfigurationState) && query.ConfigurationState is not ("UNCONFIGURED" or "ENABLED" or "DISABLED"))
            errors["configurationState"] = ["Trạng thái cấu hình không hợp lệ."];
        if (!string.IsNullOrEmpty(query.TriggerKind) && query.TriggerKind is not ("SYSTEM" or "MANUAL"))
            errors["triggerKind"] = ["Loại sự kiện không hợp lệ."];
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailConfigurationListItem>>(errors);
        var events = EventRows(schoolId).AsNoTracking();
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) events = events.Where(e => e.Code.Contains(search) || e.Name.Contains(search));
        if (!string.IsNullOrEmpty(query.TriggerKind)) events = events.Where(e => e.TriggerKind == query.TriggerKind);
        var configurations = db.NotificationConfigs.AsNoTracking().Where(c => c.SchoolId == schoolId && c.SchoolBranchId == null && c.BaseConfigId == null);
        var rows = from e in events
                   join c in configurations on new { EventCode = e.Code, Code = "EMAIL_" + e.Code } equals new { c.EventCode, c.Code } into matches
                   from c in matches.DefaultIfEmpty()
                   select new { Event = e, Config = c };
        if (query.ConfigurationState == "UNCONFIGURED") rows = rows.Where(x => x.Config == null);
        if (query.ConfigurationState == "ENABLED") rows = rows.Where(x => x.Config != null && x.Config.IsActive);
        if (query.ConfigurationState == "DISABLED") rows = rows.Where(x => x.Config != null && !x.Config.IsActive);
        return await PageAsync(rows.OrderBy(x => x.Event.Name).ThenBy(x => x.Event.Id).Select(x => new EmailConfigurationListItem(
            x.Event.Code, x.Event.Name, x.Event.TriggerKind, x.Event.Status,
            x.Config == null ? null : x.Config.Id, x.Config == null ? 0 : x.Config.Version,
            x.Config == null ? "UNCONFIGURED" : x.Config.IsActive ? "ENABLED" : "DISABLED",
            x.Config == null || x.Config.EmailTemplateVersion == null ? null : x.Config.EmailTemplateVersion.Template.Name,
            x.Config == null || x.Config.EmailTemplateVersion == null ? null : x.Config.EmailTemplateVersion.Revision,
            x.Config == null || x.Config.EmailTemplateVersion == null ? null : x.Config.EmailTemplateVersion.Template.Status,
            x.Config == null ? 0 : x.Config.Targets.Count)), query, ct);
    }

    public async Task<ServiceResult<EmailConfigItem>> ConfigurationAsync(ulong schoolId, string eventCode, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<EmailConfigItem>();
        if (!await EventRows(schoolId).AnyAsync(e => e.Code == eventCode, ct)) return FieldError<EmailConfigItem>("eventCode", "Sự kiện không hợp lệ.");
        return ServiceResult<EmailConfigItem>.Success(await ReadConfigAsync(schoolId, eventCode, ct));
    }

    public async Task<ServiceResult<EmailConfigItem>> SaveConfigurationAsync(ulong schoolId, string eventCode, SaveEmailConfigRequest request, ulong actor, CancellationToken ct)
    {
        if (!await EventRows(schoolId).AnyAsync(e => e.Code == eventCode, ct)) return FieldError<EmailConfigItem>("eventCode", "Sự kiện không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AuthorizeWriteAsync(schoolId, actor, false, ct)) return Denied<EmailConfigItem>();
        var config = await ConfigRows(schoolId, eventCode).Include(c => c.Targets).SingleOrDefaultAsync(ct);
        if ((config == null && request.Version != 0) || (config != null && (request.Version == 0 || config.Version != request.Version)))
            return Conflict<EmailConfigItem>();
        // Turning off an existing rule must remain possible after a recipient becomes inactive.
        // This shortcut preserves every saved reference; changing references still validates them.
        if (config != null && !request.IsActive && request.EmailTemplateVersionId == config.EmailTemplateVersionId
            && request.RecipientScope == config.RecipientScope && request.Targets != null
            && request.Targets.Select(t => (t?.RoleId, t?.UserId, t?.Action)).OrderBy(t => t).SequenceEqual(
                config.Targets.Select(t => ((ulong?)t.RoleId, (ulong?)t.UserId, (string?)t.Action)).OrderBy(t => t)))
        {
            var snapshot = ConfigSnapshot(config);
            config.IsActive = false;
            config.Version++;
            Audit(actor, "EMAIL_CONFIG", config.Id, "UPDATE", new { Before = snapshot, After = ConfigSnapshot(config) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ServiceResult<EmailConfigItem>.Success(await ReadConfigAsync(schoolId, eventCode, ct));
        }
        if (request.Targets == null) return FieldError<EmailConfigItem>("targets", "Danh sách lựa chọn người nhận không hợp lệ.");
        var errors = await recipients.ValidateAsync(schoolId, request, ct);
        if (errors.Count > 0) return Invalid<EmailConfigItem>(errors);
        var revision = await db.EmailTemplateVersions.Include(r => r.Template).SingleOrDefaultAsync(r =>
            r.Id == request.EmailTemplateVersionId && r.Template.SchoolId == schoolId && r.Template.EventCode == eventCode, ct);
        if (revision == null || (request.IsActive && revision.Template.Status != "ACTIVE"))
            return FieldError<EmailConfigItem>("emailTemplateVersionId", "Chọn phiên bản mẫu phù hợp, đang áp dụng trong trường.");
        if (request.IsActive && !await recipients.Resolve(schoolId, request).AnyAsync(ct))
            return FieldError<EmailConfigItem>("targets", "Cấu hình đang áp dụng phải có ít nhất một người nhận hợp lệ.");
        if (request.IsActive && !await EventRows(schoolId).AnyAsync(e => e.Code == eventCode && e.Status == "ACTIVE", ct))
            return FieldError<EmailConfigItem>("eventCode", "Sự kiện đã ngừng áp dụng.");
        var before = config == null ? null : ConfigSnapshot(config);
        if (config == null)
        {
            config = new NotificationConfig { SchoolId = schoolId, Code = "EMAIL_" + eventCode, EventCode = eventCode, Timing = "IMMEDIATE" };
            db.NotificationConfigs.Add(config);
        }
        else
        {
            config.Version++;
            foreach (var target in config.Targets.Where(t => !request.Targets.Any(r => r.RoleId == t.RoleId && r.UserId == t.UserId)).ToArray())
            {
                db.NotificationTargets.Remove(target);
                config.Targets.Remove(target);
            }
        }
        config.EmailTemplateVersionId = revision.Id;
        config.TitleTemplate = revision.Subject;
        config.ContentTemplate = revision.Body;
        config.RecipientScope = request.RecipientScope;
        config.IsActive = request.IsActive;
        foreach (var target in request.Targets)
        {
            var existing = config.Targets.SingleOrDefault(t => t.RoleId == target.RoleId && t.UserId == target.UserId);
            if (existing != null) existing.Action = target.Action;
            else config.Targets.Add(new NotificationTarget { RoleId = target.RoleId, UserId = target.UserId, Action = target.Action });
        }
        revision.Template.UsedAt ??= DateTime.UtcNow;
        var roleIds = request.Targets.Where(t => t.RoleId.HasValue).Select(t => t.RoleId!.Value).ToArray();
        if (roleIds.Length > 0)
            await db.Roles.Where(r => roleIds.Contains(r.Id) && r.UsedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.UsedAt, DateTime.UtcNow).SetProperty(r => r.Version, r => r.Version + 1), ct);
        await db.SaveChangesAsync(ct);
        Audit(actor, "EMAIL_CONFIG", config.Id, before == null ? "CREATE" : "UPDATE", new { Before = before, After = ConfigSnapshot(config) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<EmailConfigItem>.Success(await ReadConfigAsync(schoolId, eventCode, ct));
    }

    public async Task<ServiceResult<DirectoryPage<EmailRecipientOption>>> RecipientsAsync(ulong schoolId, string kind, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailRecipientOption>>();
        var errors = EmailTemplateRules.Query(query);
        if (kind is not ("role" or "user")) errors["kind"] = ["Loại người nhận không hợp lệ."];
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailRecipientOption>>(errors);
        var search = query.Search?.Trim();
        if (kind == "role")
        {
            var roles = db.Roles.AsNoTracking().Where(r => r.Status == "ACTIVE" && (!r.SchoolId.HasValue || r.SchoolId == schoolId)
                && (!r.SchoolBranchId.HasValue || (r.SchoolBranch!.SchoolId == schoolId && r.SchoolBranch.Status == "ACTIVE")));
            if (query.BranchId.HasValue) roles = roles.Where(r => !r.SchoolBranchId.HasValue || r.SchoolBranchId == query.BranchId);
            if (!string.IsNullOrEmpty(search)) roles = roles.Where(r => r.Name.Contains(search) || r.Code.Contains(search));
            return await PageAsync(roles.OrderBy(r => r.Name).ThenBy(r => r.Id)
                .Select(r => new EmailRecipientOption(r.Id, r.Name, null, r.SchoolBranch == null ? null : r.SchoolBranch.Name, r.Code)), query, ct);
        }
        return await UserPageAsync(recipients.Resolve(schoolId, new SaveEmailConfigRequest { RecipientScope = "ALL_SCHOOL" }), query, ct);
    }

    public async Task<ServiceResult<DirectoryPage<EmailRecipientOption>>> PreviewAsync(ulong schoolId, SaveEmailConfigRequest request, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailRecipientOption>>();
        var errors = EmailTemplateRules.Query(query);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailRecipientOption>>(errors);
        errors = await recipients.ValidateAsync(schoolId, request, ct);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailRecipientOption>>(errors);
        return await UserPageAsync(recipients.Resolve(schoolId, request), query, ct);
    }

    public async Task<ServiceResult<DirectoryPage<EmailHistoryItem>>> HistoryAsync(ulong schoolId, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailHistoryItem>>();
        var errors = ListErrors(query, true);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailHistoryItem>>(errors);
        var rows = HistoryRows(schoolId);
        if (!string.IsNullOrEmpty(query.SendKind)) rows = rows.Where(n => n.SendKind == query.SendKind);
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(n => n.Title.Contains(search));
        if (!string.IsNullOrEmpty(query.EventCode)) rows = rows.Where(n => n.EventCode == query.EventCode);
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(n => n.Recipients.Any(r => r.EmailStatus == query.Status));
        if (query.BranchId.HasValue) rows = rows.Where(n => n.SchoolBranchId == query.BranchId);
        return await PageAsync(rows.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Select(HistoryProjection), query, ct);
    }

    public async Task<ServiceResult<EmailHistoryDetail>> HistoryDetailAsync(ulong schoolId, ulong id, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<EmailHistoryDetail>();
        var item = await HistoryRows(schoolId).Where(n => n.Id == id).Select(HistoryProjection).SingleOrDefaultAsync(ct);
        if (item == null) return Missing<EmailHistoryDetail>();
        var content = await HistoryRows(schoolId).Where(n => n.Id == id).Select(n => new { n.Content, n.ActionUrl }).SingleAsync(ct);
        return ServiceResult<EmailHistoryDetail>.Success(new(item, content.Content, content.ActionUrl));
    }

    public async Task<ServiceResult<DirectoryPage<EmailDeliveryItem>>> DeliveriesAsync(ulong schoolId, ulong id, EmailListQuery query, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, false, ct)) return Denied<DirectoryPage<EmailDeliveryItem>>();
        var errors = ListErrors(query, true);
        if (errors.Count > 0) return Invalid<DirectoryPage<EmailDeliveryItem>>(errors);
        if (!await HistoryRows(schoolId).AnyAsync(n => n.Id == id, ct)) return Missing<DirectoryPage<EmailDeliveryItem>>();
        var rows = db.NotificationRecipients.AsNoTracking().Where(r => r.Notification.SchoolId == schoolId && r.NotificationId == id);
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(r => r.User.FullName.Contains(search) || (r.EmailAddress != null && r.EmailAddress.Contains(search)));
        if (!string.IsNullOrEmpty(query.Status)) rows = rows.Where(r => r.EmailStatus == query.Status);
        if (query.BranchId.HasValue) rows = rows.Where(r => r.User.SchoolBranchId == query.BranchId);
        return await PageAsync(rows.OrderBy(r => r.Id).Select(r => new EmailDeliveryItem(r.Id, r.UserId, r.User.FullName,
            r.EmailAddress, r.EmailStatus, r.AttemptCount, r.LastError, r.EmailSentAt)), query, ct);
    }

    public async Task<ServiceResult<EmailQueueResult>> TestAsync(ulong schoolId, SendTestEmailRequest request, ulong actor, CancellationToken ct)
    {
        if (!await AllowedAsync(schoolId, actor, true, ct)) return Denied<EmailQueueResult>();
        return await notifications.QueueTestAsync(schoolId, request, actor, ct);
    }

    private async Task<ServiceResult<EmailTemplateDetail>> ReadTemplateAsync(ulong schoolId, ulong id, CancellationToken ct)
    {
        var item = await db.EmailTemplates.AsNoTracking().Where(t => t.SchoolId == schoolId && t.Id == id).Select(TemplateProjection).SingleOrDefaultAsync(ct);
        if (item == null) return Missing<EmailTemplateDetail>();
        var revision = await db.EmailTemplateVersions.AsNoTracking().Where(r => r.Template.SchoolId == schoolId && r.Id == item.LatestRevisionId)
            .Select(RevisionProjection).SingleAsync(ct);
        return ServiceResult<EmailTemplateDetail>.Success(new(item, revision));
    }

    private IQueryable<NotificationConfig> ConfigRows(ulong schoolId, string eventCode) => db.NotificationConfigs
        .Where(c => c.SchoolId == schoolId && c.SchoolBranchId == null && c.BaseConfigId == null && c.Code == "EMAIL_" + eventCode && c.EventCode == eventCode);

    private async Task<EmailConfigItem> ReadConfigAsync(ulong schoolId, string eventCode, CancellationToken ct)
    {
        var config = await ConfigRows(schoolId, eventCode).AsNoTracking().Select(c => new { c.Id, c.EmailTemplateVersionId, c.Version, c.IsActive, c.RecipientScope,
            TemplateName = c.EmailTemplateVersion == null ? null : c.EmailTemplateVersion.Template.Name,
            Revision = c.EmailTemplateVersion == null ? (uint?)null : c.EmailTemplateVersion.Revision,
            VariablesJson = c.EmailTemplateVersion == null ? null : c.EmailTemplateVersion.VariablesJson,
            TemplateStatus = c.EmailTemplateVersion == null ? null : c.EmailTemplateVersion.Template.Status }).SingleOrDefaultAsync(ct);
        if (config == null) return new(null, eventCode, null, 0, false, "NONE", []);
        var targets = await db.NotificationTargets.AsNoTracking().Where(t => t.ConfigId == config.Id).OrderBy(t => t.Id)
            .Select(t => new EmailTargetItem(t.RoleId, t.UserId, t.Action, t.Role != null ? t.Role.Name : t.User!.FullName)).ToArrayAsync(ct);
        return new(config.Id, eventCode, config.EmailTemplateVersionId, config.Version, config.IsActive, config.RecipientScope, targets,
            config.TemplateName, config.Revision, config.TemplateStatus,
            config.EmailTemplateVersionId == null ? null : EmailTemplateRules.Variables(config.VariablesJson, eventCode));
    }

    private IQueryable<Notification> HistoryRows(ulong schoolId) => db.Notifications.AsNoTracking()
        .Where(n => n.SchoolId == schoolId && n.EmailTemplateVersionId != null);

    private async Task<ServiceResult<DirectoryPage<EmailRecipientOption>>> UserPageAsync(IQueryable<User> rows, EmailListQuery query, CancellationToken ct)
    {
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) rows = rows.Where(u => u.FullName.Contains(search) || u.Username.Contains(search) || u.Email.Contains(search));
        if (query.BranchId.HasValue) rows = rows.Where(u => u.SchoolBranchId == query.BranchId);
        return await PageAsync(rows.OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Select(u => new EmailRecipientOption(u.Id, u.FullName, u.Email, u.SchoolBranch!.Name, u.Username)), query, ct);
    }

    private async Task<(bool GlobalAdmin, bool SchoolAdmin, ulong? SchoolId)> AccessAsync(ulong actor, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == actor && u.Status == "ACTIVE")
            .Select(u => new { SchoolId = u.SchoolBranch != null && u.SchoolBranch.Status == "ACTIVE" && u.SchoolBranch.School.Status == "ACTIVE" ? (ulong?)u.SchoolBranch.SchoolId : null })
            .SingleOrDefaultAsync(ct);
        if (user == null) return (false, false, null);
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == actor).WhereEffective()
            .Select(r => new { r.Role.Code, r.Role.SchoolId, r.Role.SchoolBranchId }).ToArrayAsync(ct);
        var global = roles.Any(r => IsAdministrator(r.Code.Trim()) && !r.SchoolId.HasValue && !r.SchoolBranchId.HasValue);
        var schoolAdmin = user.SchoolId.HasValue && roles.Any(r => IsAdministrator(r.Code.Trim()) && r.SchoolId == user.SchoolId && !r.SchoolBranchId.HasValue);
        var pht = user.SchoolId.HasValue && roles.Any(r => roleCatalog.IsPht(r.Code.Trim()) && !r.SchoolBranchId.HasValue && (!r.SchoolId.HasValue || r.SchoolId == user.SchoolId));
        return (global, schoolAdmin, schoolAdmin || pht ? user.SchoolId : null);
    }

    private async Task<bool> AllowedAsync(ulong schoolId, ulong actor, bool requireAdmin, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct);
        return (access.GlobalAdmin || (access.SchoolId == schoolId && (!requireAdmin || access.SchoolAdmin)))
            && await db.Schools.AnyAsync(s => s.Id == schoolId && s.Status == "ACTIVE", ct);
    }

    private async Task<bool> AuthorizeWriteAsync(ulong schoolId, ulong actor, bool requireAdmin, CancellationToken ct)
    {
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actor} FOR UPDATE").ToArrayAsync(ct);
        if (!await AllowedAsync(schoolId, actor, requireAdmin, ct)) return false;
        // One school lock serializes first-time configuration creation and template revision allocation.
        await db.Schools.FromSqlInterpolated($"SELECT * FROM schools WHERE id = {schoolId} FOR UPDATE").ToArrayAsync(ct);
        return true;
    }

    private static Dictionary<string, string[]> ListErrors(EmailListQuery query, bool history)
    {
        var errors = EmailTemplateRules.Query(query);
        if (!string.IsNullOrEmpty(query.Status) && !(history
            ? query.Status is "PENDING" or "SENDING" or "SENT" or "ERROR" or "CANCELLED"
            : query.Status is "ACTIVE" or "INACTIVE")) errors["status"] = ["Trạng thái không hợp lệ."];
        return errors;
    }

    private static async Task<ServiceResult<DirectoryPage<T>>> PageAsync<T>(IQueryable<T> rows, EmailListQuery query, CancellationToken ct)
    {
        var total = await rows.CountAsync(ct);
        var items = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return ServiceResult<DirectoryPage<T>>.Success(new(items, query.Page, query.PageSize, total));
    }

    private static ServiceResult<T> Denied<T>() => ServiceResult<T>.Failure("FORBIDDEN", "Bạn không có quyền quản lý email tại trường này.");
    private static ServiceResult<T> FieldError<T>(string field, string message) => Invalid<T>(new Dictionary<string, string[]> { [field] = [message] });
    private static object TemplateSnapshot(EmailTemplate template) => new { template.SchoolId, template.Code, template.Name, template.EventCode, template.Status, template.Version };
    private static object ConfigSnapshot(NotificationConfig config) => new
    {
        config.SchoolId, config.EventCode, config.EmailTemplateVersionId, config.IsActive, config.RecipientScope, config.Version,
        Targets = config.Targets.Select(t => new { t.RoleId, t.UserId, t.Action }).ToArray()
    };
    private void Audit(ulong actor, string type, ulong id, string action, object snapshot) => db.IdentityAudits.Add(new IdentityAudit
    { ActorUserId = actor, EntityType = type, EntityId = id, Action = action, Data = JsonSerializer.Serialize(snapshot) });
}
