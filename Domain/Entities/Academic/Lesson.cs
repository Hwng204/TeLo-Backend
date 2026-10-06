namespace Domain.Entities.Academic;

public sealed class Lesson
{
    public ulong Id { get; set; }
    public ulong ChapterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public uint SortOrder { get; set; }

    public Chapter Chapter { get; set; } = null!;
}
