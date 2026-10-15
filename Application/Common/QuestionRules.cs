namespace Application.Common;

public static class QuestionRules
{
    public static readonly string[] Keys = ["A", "B", "C", "D"];
    public static bool Editable(string status) => status is "ASSIGNED" or "IN_PROGRESS" or "NEEDS_REVISION";
    public static bool IsComplete(string? content, string[]? options, string? answer)
    {
        if (string.IsNullOrWhiteSpace(content) || options is not { Length: 4 } || options.Any(x => x is null)) return false;
        var used = options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        var answerIndex = Array.IndexOf(Keys, answer);
        return used.Length >= 2 && used.Distinct(StringComparer.Ordinal).Count() == used.Length &&
            answerIndex >= 0 && !string.IsNullOrWhiteSpace(options[answerIndex]);
    }

    public static Dictionary<string, string[]> DraftErrors(Application.DTOs.SaveQuestionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.DetailId == 0) errors["detailId"] = ["Chọn bài học và mức nhận thức."];
        if (request.Content?.Length > 10000) errors["content"] = ["Nội dung tối đa 10.000 ký tự."];
        if (request.Options is not { Length: 4 } || request.Options.Any(x => x is null || x.Length > 2000))
            errors["options"] = ["Cần bốn ô A–D, mỗi ô tối đa 2.000 ký tự. Ô không dùng để trống."];
        if (request.CorrectOption is not null && !Keys.Contains(request.CorrectOption)) errors["correctOption"] = ["Đáp án đúng phải là A, B, C hoặc D."];
        if (request.Explanation?.Length > 6000) errors["explanation"] = ["Giải thích tối đa 6.000 ký tự."];
        return errors;
    }

    public static bool Ready(IEnumerable<(int Required, int Valid)> counts)
    {
        var rows = counts.ToArray();
        return rows.Length > 0 && rows.All(x => x.Required == x.Valid);
    }
}
