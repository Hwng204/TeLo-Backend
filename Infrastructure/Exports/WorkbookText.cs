namespace Infrastructure.Exports;

public static class WorkbookText
{
    // Neutralises spreadsheet formula injection: a cell that starts with = + - or @ would be
    // evaluated by Excel, so it is written as text with a leading apostrophe.
    public static string Safe(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@'
            ? $"'{normalized}"
            : normalized;
    }
}
