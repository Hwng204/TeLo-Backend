using System.Text.RegularExpressions;
using System.Globalization;
using System.Text.Json;
using Application.DTOs;
using Domain.Entities.Notification;

namespace Application.Common;

public static partial class EmailTemplateRules
{
    // Compatibility for legacy revisions and unit tests; runtime catalog comes from email_events.
    public static IReadOnlyList<EmailEventItem> SystemEvents { get; } = [
        new("MATRIX_ASSIGNED", "Giao nhiệm vụ lập ma trận", ["schoolName", "branchName", "actorName", "taskName", "dueAt", "actionUrl"]),
        new("MATRIX_SUBMITTED", "Ma trận đã nộp", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"]),
        new("MATRIX_APPROVED", "Ma trận đã duyệt / hoàn tất", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"]),
        new("MATRIX_REJECTED", "Ma trận cần chỉnh sửa", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"])
    ];

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}")]
    private static partial Regex VariablePattern();
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{1,99}$")]
    private static partial Regex CodePattern();

    public static Dictionary<string, string[]> Query(EmailListQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page is < 1 or > 1_000_000) errors["page"] = ["Trang không hợp lệ."];
        if (query.PageSize is < 1 or > 100) errors["pageSize"] = ["Kích thước trang từ 1 đến 100."];
        if (query.Search?.Length > 150) errors["search"] = ["Từ khóa tối đa 150 ký tự."];
        if (query.BranchId == 0) errors["branchId"] = ["Phân hiệu không hợp lệ."];
        if (query.SchoolId == 0) errors["schoolId"] = ["Trường không hợp lệ."];
        if (query.EventCode?.Length > 100) errors["eventCode"] = ["Mã sự kiện tối đa 100 ký tự."];
        if (!string.IsNullOrEmpty(query.SendKind) && query.SendKind is not ("MANUAL" or "AUTOMATIC" or "TEST")) errors["sendKind"] = ["Loại gửi không hợp lệ."];
        return errors;
    }

    public static Dictionary<string, string[]> ValidateTemplate(SaveEmailTemplateRequest request, EmailEventItem? eventDefinition = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CodePattern().IsMatch(request.Code?.Trim() ?? "")) errors["code"] = ["Mã gồm 2–100 ký tự chữ, số, gạch ngang hoặc gạch dưới."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150) errors["name"] = ["Tên bắt buộc, tối đa 150 ký tự."];
        var definition = eventDefinition ?? SystemEvents.SingleOrDefault(e => e.Code == request.EventCode);
        if (definition == null) errors["eventCode"] = ["Sự kiện không hợp lệ."];
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200 || request.Subject.Contains('\r') || request.Subject.Contains('\n'))
            errors["subject"] = ["Tiêu đề bắt buộc, tối đa 200 ký tự, không xuống dòng."];
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 10_000) errors["body"] = ["Nội dung bắt buộc, tối đa 10.000 ký tự."];
        if (definition != null)
        {
            if (!ValidVariables(request.Subject ?? "", definition)) errors["subject"] = ["Biến trong tiêu đề không hợp lệ; dùng cú pháp {{tenBien}} và biến của sự kiện."];
            if (!ValidVariables(request.Body ?? "", definition)) errors["body"] = ["Biến trong nội dung không hợp lệ; dùng cú pháp {{tenBien}} và biến của sự kiện."];
        }
        return errors;
    }

    private static bool ValidVariables(string text, EmailEventItem definition) =>
        VariablePattern().Matches(text).All(m => definition.Variables.Contains(m.Groups[1].Value)) &&
        !VariablePattern().Replace(text, "").Contains("{{", StringComparison.Ordinal) &&
        !VariablePattern().Replace(text, "").Contains("}}", StringComparison.Ordinal);

    public static string Render(string text, string eventCode, IReadOnlyDictionary<string, string> values)
    {
        var definition = SystemEvents.Single(e => e.Code == eventCode);
        return Render(text, definition, values);
    }

    public static string Render(string text, EmailEventItem definition, IReadOnlyDictionary<string, string> values)
    {
        if (!ValidVariables(text, definition)) throw new InvalidOperationException("Invalid email variables.");
        return VariablePattern().Replace(text, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value : throw new InvalidOperationException("Missing email variable."));
    }

    public static readonly EmailEventVariable[] CommonVariables = [
        new("schoolName", "Tên trường"), new("actorName", "Người gửi"), new("actionUrl", "Liên kết", "URL", false)
    ];

    public static EmailEventVariable[] Variables(string? json, string code) => json == null
        ? SystemEvents.Single(e => e.Code == code).Variables.Select(name => new EmailEventVariable(name, name)).ToArray()
        : JsonSerializer.Deserialize<EmailEventVariable[]>(json) ?? [];

    public static EmailEventItem Definition(EmailEvent row)
    {
        var variables = Variables(row.VariablesJson, row.Code);
        return new(row.Code, row.Name, variables.Select(v => v.Name).ToArray(), row.Id, row.TriggerKind,
            row.Status, row.Version, row.Description, variables, row.TriggerKind == "MANUAL" && row.UsedAt == null);
    }

    public static EmailEventItem RevisionDefinition(EmailTemplateVersion revision)
    {
        var variables = Variables(revision.VariablesJson, revision.Template.EventCode);
        return new(revision.Template.EventCode, revision.Template.Name, variables.Select(v => v.Name).ToArray(),
            VariableDefinitions: variables);
    }

    public static Dictionary<string, string[]> ValidateEvent(SaveEmailEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CodePattern().IsMatch(request.Code?.Trim() ?? "") || request.Code?.Trim().Length > 94) errors["code"] = ["Mã gồm 2–94 ký tự chữ, số, gạch ngang hoặc gạch dưới."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150) errors["name"] = ["Tên bắt buộc, tối đa 150 ký tự."];
        if (request.Description == null || request.Description.Length > 1000) errors["description"] = ["Mô tả tối đa 1.000 ký tự."];
        var variables = request.VariableDefinitions;
        if (variables == null || variables.Length > 20 || variables.Any(v => v == null ||
            !Regex.IsMatch(v.Name ?? "", @"^[A-Za-z][A-Za-z0-9_]{0,31}$") ||
            string.IsNullOrWhiteSpace(v.Label) || v.Label.Length > 150 || v.Type is not ("TEXT" or "DATE" or "NUMBER" or "URL") ||
            CommonVariables.Any(c => c.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) ||
            variables.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variables.Length)
            errors["variableDefinitions"] = ["Tối đa 20 biến riêng, tên không trùng (kể cả biến hệ thống), có nhãn và kiểu hợp lệ."];
        return errors;
    }

    public static Dictionary<string, string[]> ValidateValues(EmailEventItem definition, IReadOnlyDictionary<string, string>? values)
    {
        var errors = new Dictionary<string, string[]>();
        var variables = definition.VariableDefinitions ?? [];
        if (values == null || values.Count > 30 || values.Keys.Any(k => !variables.Any(v => v.Name == k)))
        { errors["values"] = ["Dữ liệu chứa biến không được phép."]; return errors; }
        foreach (var variable in variables)
        {
            var value = values.GetValueOrDefault(variable.Name);
            if (string.IsNullOrWhiteSpace(value))
            { if (variable.Required) errors[$"values.{variable.Name}"] = [$"Nhập {variable.Label}."]; continue; }
            if (value.Length > 2000 || (variable.Type == "DATE" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) ||
                (variable.Type == "NUMBER" && !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out _)) ||
                (variable.Type == "URL" && (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo))))
                errors[$"values.{variable.Name}"] = [$"{variable.Label} không đúng kiểu hoặc vượt 2.000 ký tự."];
        }
        return errors;
    }
}
