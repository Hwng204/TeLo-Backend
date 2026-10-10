using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Notification;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotificationEntity = Domain.Entities.Notification.Notification;

namespace Application.Services.Implement;

public sealed class EmailNotificationService(ApplicationDbContext db, EmailRecipientResolver recipients,
    IEmailService sender, IConfiguration configuration) : IEmailNotificationService
{
    public async Task<ServiceResult<EmailQueueResult>> QueueTestAsync(ulong schoolId, SendTestEmailRequest request, ulong actor, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty) return ServiceResult<EmailQueueResult>.Failure("VALIDATION_ERROR", "Mã yêu cầu gửi thử không hợp lệ.");
        if (!bool.TryParse(configuration["NotificationEmail:Enabled"], out var enabled) || !enabled)
            return ServiceResult<EmailQueueResult>.Failure("UNAVAILABLE", "Chưa bật dịch vụ gửi email. Hãy cấu hình SMTP và NotificationEmail:Enabled trên máy chủ.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {actor} FOR UPDATE").ToArrayAsync(ct);
        if (!await db.UserRoles.WhereEffective().AnyAsync(r => r.UserId == actor && r.User.Status == "ACTIVE" &&
            (r.Role.Code == "ADMIN" || r.Role.Code == "OperationalAdmin") && (r.Role.SchoolId == null || r.Role.SchoolId == schoolId) &&
            r.Role.SchoolBranchId == null, ct))
            return ServiceResult<EmailQueueResult>.Failure("FORBIDDEN", "Bạn không có quyền gửi thử trong trường này.");
        var key = $"test:{schoolId}:{actor}:{request.RequestId:N}";
        var existing = await db.Notifications.Include(n => n.Recipients).SingleOrDefaultAsync(n => n.EventKey == key, ct);
        if (existing != null)
        {
            if (existing.EmailTemplateVersionId != request.RevisionId || !existing.Recipients.Any(r => r.UserId == request.RecipientUserId))
                return ServiceResult<EmailQueueResult>.Failure("CONFLICT", "Mã yêu cầu đã được dùng cho một lần gửi khác.");
            return ServiceResult<EmailQueueResult>.Success(new(existing.Id, existing.Recipients.Single().EmailStatus));
        }
        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.Notifications.CountAsync(n => n.IsTest && n.CreatedByUserId == actor && n.CreatedAt >= since, ct) >= 10)
            return ServiceResult<EmailQueueResult>.Failure("RATE_LIMITED", "Tối đa 10 email gửi thử mỗi giờ.");
        var revision = await db.EmailTemplateVersions.Include(r => r.Template)
            .SingleOrDefaultAsync(r => r.Id == request.RevisionId && r.Template.SchoolId == schoolId && r.Template.Status == "ACTIVE", ct);
        var user = await recipients.EligibleUsers(schoolId).SingleOrDefaultAsync(u => u.Id == request.RecipientUserId, ct);
        if (revision == null || user == null || !ValidEmail(user.Email))
            return ServiceResult<EmailQueueResult>.Failure("VALIDATION_ERROR", "Chọn phiên bản mẫu đang áp dụng và người nhận có email hợp lệ trong trường.");
        if (!await db.EmailEvents.AnyAsync(e => e.Code == revision.Template.EventCode && (e.SchoolId == null || e.SchoolId == schoolId) && e.Status == "ACTIVE", ct))
            return ServiceResult<EmailQueueResult>.Failure("VALIDATION_ERROR", "Sự kiện đã ngừng áp dụng.");
        var schoolName = await db.Schools.Where(s => s.Id == schoolId).Select(s => s.Name).SingleAsync(ct);
        var values = new Dictionary<string, string>
        {
            ["schoolName"] = schoolName, ["branchName"] = "Phân hiệu mẫu", ["actorName"] = "Người gửi thử",
            ["matrixName"] = "Ma trận minh họa", ["taskName"] = "Nhiệm vụ minh họa", ["dueAt"] = "31/12/2030", ["actionUrl"] = ActionUrl("/emails")
        };
        foreach (var variable in EmailTemplateRules.Variables(revision.VariablesJson, revision.Template.EventCode))
            values.TryAdd(variable.Name, variable.Type == "DATE" ? "2030-12-31" : variable.Type == "NUMBER" ? "1" : variable.Type == "URL" ? "https://example.invalid/notice" : "Sample value");
        var notification = CreateNotification(schoolId, null, revision, revision.Template.EventCode, key, actor, values);
        notification.Title = "[GỬI THỬ] " + notification.Title;
        notification.IsTest = true; notification.SendKind = "TEST";
        notification.Recipients.Add(new NotificationRecipient { UserId = user.Id, EmailAddress = user.Email.Trim(), NextAttemptAt = DateTime.UtcNow });
        revision.Template.UsedAt ??= DateTime.UtcNow;
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<EmailQueueResult>.Success(new(notification.Id, "PENDING"));
    }

    public async Task QueueMatrixAsync(ulong matrixId, string eventCode, ulong actor, CancellationToken ct)
    {
        var transactionId = db.Database.CurrentTransaction?.TransactionId
            ?? throw new InvalidOperationException("Matrix notifications require the business transaction.");
        var matrix = await db.ExamMatrices.AsNoTracking().Where(m => m.Id == matrixId)
            .Select(m => new { m.Name, m.AcademicContext.SchoolId, m.AcademicContext.SchoolBranchId,
                SchoolName = m.AcademicContext.School.Name, BranchName = m.AcademicContext.SchoolBranch.Name }).SingleAsync(ct);
        await QueueEventAsync(matrix.SchoolId, matrix.SchoolBranchId, eventCode, $"matrix:{matrixId}:{eventCode}:{transactionId:N}", actor,
            new Dictionary<string, string> { ["schoolName"] = matrix.SchoolName, ["branchName"] = matrix.BranchName,
                ["matrixName"] = matrix.Name, ["actionUrl"] = ActionUrl($"/matrices/{matrixId}") }, ct);
    }

    public async Task QueueMatrixAssignmentAsync(ulong taskId, ulong actor, CancellationToken ct)
    {
        var task = await (from t in db.WorkTasks.AsNoTracking()
            join context in db.AcademicContexts on t.AcademicContextId equals context.Id
            where t.Id == taskId
            select new { t.Name, t.DueAt, context.SchoolId, context.SchoolBranchId,
                SchoolName = context.School.Name, BranchName = context.SchoolBranch.Name }).SingleAsync(ct);
        await QueueEventAsync(task.SchoolId, task.SchoolBranchId, "MATRIX_ASSIGNED", $"matrix-task:{taskId}:assigned", actor,
            new Dictionary<string, string> { ["schoolName"] = task.SchoolName, ["branchName"] = task.BranchName,
                ["taskName"] = task.Name, ["dueAt"] = task.DueAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đặt hạn",
                ["actionUrl"] = ActionUrl($"/matrix-tasks/{taskId}") }, ct);
    }

    public async Task QueueEventAsync(ulong schoolId, ulong? branchId, string eventCode, string key, ulong actor,
        Dictionary<string, string> values, CancellationToken ct)
    {
        // Status and queue are committed together by the existing business transaction.
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Email events require the business transaction.");
        if (!await db.EmailEvents.AnyAsync(e => e.SchoolId == null && e.Code == eventCode && e.TriggerKind == "SYSTEM" && e.Status == "ACTIVE", ct)) return;
        var config = await db.NotificationConfigs.Include(c => c.Targets).Include(c => c.EmailTemplateVersion).ThenInclude(v => v!.Template)
            .SingleOrDefaultAsync(c => c.SchoolId == schoolId && c.Code == "EMAIL_" + eventCode && c.SchoolBranchId == null && c.BaseConfigId == null, ct);
        if (config?.IsActive != true || config.EmailTemplateVersion?.Template.Status != "ACTIVE" ||
            config.EmailTemplateVersion.Template.SchoolId != schoolId || config.EmailTemplateVersion.Template.EventCode != eventCode) return;
        if (await db.Notifications.AnyAsync(n => n.EventKey == key, ct)) return;
        var rule = new SaveEmailConfigRequest { RecipientScope = config.RecipientScope,
            Targets = config.Targets.Select(t => new EmailTargetItem(t.RoleId, t.UserId, t.Action)).ToArray() };
        values["actorName"] = await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(ct);
        var notification = CreateNotification(schoolId, branchId, config.EmailTemplateVersion, eventCode, key, actor, values);
        notification.ConfigId = config.Id;
        ulong lastId = 0;
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var batch = await recipients.Resolve(schoolId, rule).Where(u => u.Id > lastId).OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.Email }).Take(200).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            foreach (var user in batch)
            {
                var valid = ValidEmail(user.Email);
                var duplicate = valid && !emails.Add(user.Email.Trim());
                notification.Recipients.Add(new NotificationRecipient { UserId = user.Id, EmailAddress = user.Email.Trim(),
                    EmailStatus = !valid ? "ERROR" : duplicate ? "CANCELLED" : "PENDING", LastError = !valid ? "INVALID_EMAIL" : duplicate ? "DUPLICATE_EMAIL" : null, NextAttemptAt = DateTime.UtcNow });
            }
            lastId = batch[^1].Id;
        }
        config.EmailTemplateVersion.Template.UsedAt ??= DateTime.UtcNow;
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    private static NotificationEntity CreateNotification(ulong schoolId, ulong? branchId, EmailTemplateVersion revision,
        string eventCode, string key, ulong actor, IReadOnlyDictionary<string, string> values) => new()
    {
        SchoolId = schoolId, SchoolBranchId = branchId, EmailTemplateVersionId = revision.Id, EventCode = eventCode,
        EventKey = key, CreatedByUserId = actor, CreatedAt = DateTime.UtcNow,
        Title = LimitSubject(EmailTemplateRules.Render(revision.Subject, EmailTemplateRules.RevisionDefinition(revision), values)),
        Content = EmailTemplateRules.Render(revision.Body, EmailTemplateRules.RevisionDefinition(revision), values), ActionUrl = values.GetValueOrDefault("actionUrl")
    };

    private string ActionUrl(string path)
    {
        var origin = configuration["NotificationEmail:FrontendOrigin"];
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? origin!.TrimEnd('/') + path : path;
    }
    private static string LimitSubject(string text)
    {
        var clean = text.Replace('\r', ' ').Replace('\n', ' ');
        return clean.Length > 450 ? clean[..450] : clean;
    }
    private static bool ValidEmail(string text) => MailAddress.TryCreate(text, out var address) && address.Address == text.Trim();

    private sealed record PreparedManual(NotificationConfig Config, Dictionary<string, string> Values);

    private async Task<ServiceResult<PreparedManual>> PrepareManualAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.RequestId == Guid.Empty) errors["requestId"] = ["Mã yêu cầu không hợp lệ."];
        if (request.EventCode == null || request.EventCode.Length > 100) errors["eventCode"] = ["Mã sự kiện không hợp lệ."];
        if (request.ScheduledFor.HasValue && (request.ScheduledFor <= DateTimeOffset.UtcNow || request.ScheduledFor > DateTimeOffset.UtcNow.AddYears(1)))
            errors["scheduledFor"] = ["Lịch gửi phải ở tương lai, tối đa một năm."];
        if (request.Values == null || request.Values.Count > 20 || request.Values.Keys.Any(k => EmailTemplateRules.CommonVariables.Any(v => v.Name == k)))
            errors["values"] = ["Chỉ được nhập các biến nội dung của sự kiện; biến hệ thống được điền tự động."];
        if (errors.Count > 0) return ServiceResult<PreparedManual>.Failure("VALIDATION_ERROR", "Dữ liệu gửi không hợp lệ.", errors);
        if (!await db.EmailEvents.AnyAsync(e => e.SchoolId == schoolId && e.Code == request.EventCode && e.TriggerKind == "MANUAL" && e.Status == "ACTIVE", ct))
            return ServiceResult<PreparedManual>.Failure("VALIDATION_ERROR", "Chọn sự kiện thủ công đang áp dụng trong trường.");
        var config = await db.NotificationConfigs.Include(c => c.Targets).Include(c => c.EmailTemplateVersion).ThenInclude(v => v!.Template)
            .SingleOrDefaultAsync(c => c.SchoolId == schoolId && c.EventCode == request.EventCode && c.Code == "EMAIL_" + request.EventCode && c.SchoolBranchId == null && c.BaseConfigId == null, ct);
        if (config == null || !config.IsActive || config.EmailTemplateVersion?.Template.Status != "ACTIVE" ||
            config.EmailTemplateVersion.Template.SchoolId != schoolId || config.EmailTemplateVersion.Template.EventCode != request.EventCode)
            return ServiceResult<PreparedManual>.Failure("VALIDATION_ERROR", "Cần lưu cấu hình đang bật và chọn mẫu đang áp dụng trước khi gửi.");
        if (request.ConfigVersion == 0 || config.Version != request.ConfigVersion)
            return ServiceResult<PreparedManual>.Failure("STALE_VERSION", "Cấu hình đã thay đổi. Vui lòng tải lại trước khi gửi.");
        var values = new Dictionary<string,string>(request.Values!);
        values["schoolName"] = await db.Schools.Where(s => s.Id == schoolId).Select(s => s.Name).SingleAsync(ct);
        values["actorName"] = await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(ct);
        values["actionUrl"] = string.Empty;
        var definition = EmailTemplateRules.RevisionDefinition(config.EmailTemplateVersion);
        // Relative links are generated by the server when no public origin has been configured.
        errors = EmailTemplateRules.ValidateValues(definition, values.Where(v => v.Key != "actionUrl").ToDictionary(v => v.Key, v => v.Value));
        foreach (var variable in definition.VariableDefinitions ?? []) values.TryAdd(variable.Name, "");
        if (errors.Count > 0) return ServiceResult<PreparedManual>.Failure("VALIDATION_ERROR", "Dữ liệu biến trong mẫu không hợp lệ.", errors);
        return ServiceResult<PreparedManual>.Success(new(config, values));
    }

    public async Task<ServiceResult<EmailMessagePreview>> PreviewManualAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct)
    {
        var prepared = await PrepareManualAsync(schoolId, request, actor, ct);
        if (!prepared.IsSuccess) return ServiceResult<EmailMessagePreview>.Failure(prepared.Error!.Code, prepared.Error.Message, prepared.Error.Details);
        var value = prepared.Value!;
        var rule = new SaveEmailConfigRequest { RecipientScope = value.Config.RecipientScope,
            Targets = value.Config.Targets.Select(t => new EmailTargetItem(t.RoleId, t.UserId, t.Action)).ToArray() };
        var snapshot = await ManualRecipientsAsync(schoolId, rule, DateTime.UtcNow, ct);
        var message = CreateNotification(schoolId, null, value.Config.EmailTemplateVersion!, request.EventCode, "preview", actor, value.Values);
        return ServiceResult<EmailMessagePreview>.Success(new(message.Title, message.Content, snapshot.Count(r => r.EmailStatus == "PENDING"), PreviewFingerprint(message, snapshot)));
    }

    public async Task<ServiceResult<EmailQueueResult>> QueueManualAsync(ulong schoolId, SendEmailRequest request, ulong actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Send requires an authorized transaction.");
        if (!bool.TryParse(configuration["NotificationEmail:Enabled"], out var enabled) || !enabled)
            return ServiceResult<EmailQueueResult>.Failure("UNAVAILABLE", "Chưa bật dịch vụ gửi email. Hãy cấu hình SMTP và NotificationEmail:Enabled trên máy chủ.");
        if (string.IsNullOrWhiteSpace(configuration["EmailSettings:Email"]) || string.IsNullOrWhiteSpace(configuration["EmailSettings:Password"]))
            return ServiceResult<EmailQueueResult>.Failure("EMAIL_NOT_CONFIGURED", "Chưa cấu hình tài khoản SMTP trên máy chủ.");
        if (request.RequestId == Guid.Empty || request.Values == null || request.Values.Count > 20 || request.PreviewFingerprint?.Length > 64 || request.Values.Any(v => v.Key.Length > 32 || v.Value == null || v.Value.Length > 2000))
            return ServiceResult<EmailQueueResult>.Failure("VALIDATION_ERROR", "Mã yêu cầu hoặc dữ liệu biến không hợp lệ.");
        var key = $"manual:{schoolId}:{actor}:{request.RequestId:N}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            request.EventCode, request.ConfigVersion, request.PreviewFingerprint, ScheduledFor = request.ScheduledFor?.UtcDateTime,
            Values = request.Values.OrderBy(v => v.Key, StringComparer.Ordinal).ToArray() }))));
        var existing = await db.Notifications.SingleOrDefaultAsync(n => n.EventKey == key, ct);
        if (existing != null) return existing.RequestFingerprint == fingerprint
            ? ServiceResult<EmailQueueResult>.Success(new(existing.Id, existing.CancelledAt != null ? "CANCELLED" : "PENDING"))
            : ServiceResult<EmailQueueResult>.Failure("CONFLICT", "Mã yêu cầu đã được dùng cho nội dung hoặc lịch gửi khác.");
        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.Notifications.CountAsync(n => n.CreatedByUserId == actor && n.SendKind == "MANUAL" && n.CreatedAt >= since, ct) >= 30)
            return ServiceResult<EmailQueueResult>.Failure("RATE_LIMITED", "Tối đa 30 thông báo thủ công mỗi người gửi trong một giờ.");
        var prepared = await PrepareManualAsync(schoolId, request, actor, ct);
        if (!prepared.IsSuccess) return ServiceResult<EmailQueueResult>.Failure(prepared.Error!.Code, prepared.Error.Message, prepared.Error.Details);
        var value = prepared.Value!;
        var message = CreateNotification(schoolId, null, value.Config.EmailTemplateVersion!, request.EventCode, key, actor, value.Values);
        message.ConfigId = value.Config.Id; message.SendKind = "MANUAL";
        message.ScheduledFor = request.ScheduledFor?.UtcDateTime; message.RequestFingerprint = fingerprint;
        var rule = new SaveEmailConfigRequest { RecipientScope = value.Config.RecipientScope,
            Targets = value.Config.Targets.Select(t => new EmailTargetItem(t.RoleId, t.UserId, t.Action)).ToArray() };
        foreach (var recipient in await ManualRecipientsAsync(schoolId, rule, message.ScheduledFor ?? DateTime.UtcNow, ct)) message.Recipients.Add(recipient);
        if (request.PreviewFingerprint != null && request.PreviewFingerprint != PreviewFingerprint(message, message.Recipients))
            return ServiceResult<EmailQueueResult>.Failure("STALE_PREVIEW", "Nội dung hoặc danh sách người nhận đã thay đổi. Hãy xem trước lại để xác nhận.");
        if (!message.Recipients.Any(r => r.EmailStatus == "PENDING")) return ServiceResult<EmailQueueResult>.Failure("VALIDATION_ERROR", "Không có người nhận đủ điều kiện và email hợp lệ.");
        value.Config.EmailTemplateVersion!.Template.UsedAt ??= DateTime.UtcNow;
        db.Notifications.Add(message); await db.SaveChangesAsync(ct);
        return ServiceResult<EmailQueueResult>.Success(new(message.Id, message.ScheduledFor.HasValue ? "SCHEDULED" : "PENDING"));
    }

    private async Task<List<NotificationRecipient>> ManualRecipientsAsync(ulong schoolId, SaveEmailConfigRequest rule, DateTime due, CancellationToken ct)
    {
        var result = new List<NotificationRecipient>();
        ulong lastId = 0;
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var batch = await recipients.Resolve(schoolId, rule).Where(u => u.Id > lastId).OrderBy(u => u.Id).Select(u => new {u.Id,u.Email}).Take(200).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            foreach (var user in batch)
            {
                var valid = ValidEmail(user.Email);
                var duplicate = valid && !emails.Add(user.Email.Trim());
                result.Add(new NotificationRecipient { UserId = user.Id, EmailAddress = user.Email.Trim(), NextAttemptAt = due,
                    EmailStatus = !valid ? "ERROR" : duplicate ? "CANCELLED" : "PENDING",
                    LastError = !valid ? "INVALID_EMAIL" : duplicate ? "DUPLICATE_EMAIL" : null });
            }
            lastId = batch[^1].Id;
        }
        return result;
    }

    private static string PreviewFingerprint(NotificationEntity message, IEnumerable<NotificationRecipient> snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            message.Title, message.Content,
            Recipients = snapshot.OrderBy(r => r.UserId).Select(r => new { r.UserId, Email = r.EmailAddress!.ToUpperInvariant() }).ToArray()
        }))));

    public async Task<int> DeliverPendingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = now.AddMinutes(-5);
        var ids = await db.NotificationRecipients.AsNoTracking()
            .Where(r => r.Notification.CancelledAt == null && (r.Notification.ScheduledFor == null || r.Notification.ScheduledFor <= now) && r.EmailAddress != null && r.AttemptCount < 3 &&
                ((r.EmailStatus == "PENDING" && (r.NextAttemptAt == null || r.NextAttemptAt <= now)) ||
                 (r.EmailStatus == "SENDING" && r.LockedAt < expired)))
            .OrderBy(r => r.Id).Select(r => r.Id).Take(20).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            // Each delivery gets a fresh lease even if earlier SMTP calls were slow.
            var claimedAt = DateTime.UtcNow;
            var claimExpired = claimedAt.AddMinutes(-5);
            var token = Guid.NewGuid().ToString();
            await using var claimTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var notificationId = await db.NotificationRecipients.Where(r => r.Id == id).Select(r => r.NotificationId).SingleAsync(ct);
            var parent = (await db.Notifications.FromSqlInterpolated($"SELECT * FROM notifications WHERE id = {notificationId} FOR UPDATE").AsNoTracking().ToArrayAsync(ct)).Single();
            if (parent.CancelledAt != null || parent.ScheduledFor > claimedAt) { await claimTransaction.RollbackAsync(ct); continue; }
            var claimed = await db.NotificationRecipients.Where(r => r.Id == id && r.AttemptCount < 3 &&
                    ((r.EmailStatus == "PENDING" && (r.NextAttemptAt == null || r.NextAttemptAt <= claimedAt)) ||
                     (r.EmailStatus == "SENDING" && r.LockedAt < claimExpired)))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.EmailStatus, "SENDING").SetProperty(r => r.LockedAt, claimedAt)
                    .SetProperty(r => r.DeliveryToken, token).SetProperty(r => r.AttemptCount, r => r.AttemptCount + 1), ct);
            await claimTransaction.CommitAsync(ct);
            if (claimed == 0) continue;
            var row = await db.NotificationRecipients.AsNoTracking().Include(r => r.Notification)
                .Include(r => r.User).ThenInclude(u => u.SchoolBranch).ThenInclude(b => b!.School).SingleAsync(r => r.Id == id, ct);
            string status;
            string? error = null;
            try
            {
                if (row.User.Status != "ACTIVE" || row.User.SchoolBranch?.SchoolId != row.Notification.SchoolId ||
                    row.User.SchoolBranch.Status != "ACTIVE" || row.User.SchoolBranch.School.Status != "ACTIVE" ||
                    !string.Equals(row.User.Email.Trim(), row.EmailAddress, StringComparison.OrdinalIgnoreCase))
                { status = "CANCELLED"; error = "RECIPIENT_NO_LONGER_ELIGIBLE"; }
                else
                {
                    await sender.SendNotificationEmailAsync(row.EmailAddress!, row.Notification.Title, row.Notification.Content, ct);
                    status = "SENT";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                error = ex is InvalidOperationException ? "EMAIL_NOT_CONFIGURED" : ex is SmtpException ? "SMTP_ERROR" : "DELIVERY_ERROR";
                status = row.AttemptCount >= 3 || error == "EMAIL_NOT_CONFIGURED" ? "ERROR" : "PENDING";
            }
            var sentAt = status == "SENT" ? DateTime.UtcNow : (DateTime?)null;
            var next = DateTime.UtcNow.AddMinutes(row.AttemptCount);
            await db.NotificationRecipients.Where(r => r.Id == id && r.DeliveryToken == token && r.EmailStatus == "SENDING")
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.EmailStatus, status).SetProperty(r => r.EmailSentAt, sentAt)
                    .SetProperty(r => r.LastError, error).SetProperty(r => r.NextAttemptAt, next)
                    .SetProperty(r => r.LockedAt, (DateTime?)null).SetProperty(r => r.DeliveryToken, (string?)null), ct);
        }
        await db.NotificationRecipients.Where(r => r.EmailStatus == "SENDING" && r.AttemptCount >= 3 && r.LockedAt < expired)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.EmailStatus, "ERROR").SetProperty(r => r.LastError, "DELIVERY_INTERRUPTED"), ct);
        return ids.Length;
    }
}
