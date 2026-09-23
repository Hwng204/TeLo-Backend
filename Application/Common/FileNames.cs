namespace Application.Common;

public static class FileNames
{
    // Makes a user- or data-derived string safe to use as a download file name.
    public static string Safe(string? value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string((value ?? string.Empty)
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim('.', ' ');

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
