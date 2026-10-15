namespace Domain.Entities.Academic;

public sealed class Lesson
{
    public ulong Id { get; set; }
    public ulong ChapterId { get; set; }
    public uint Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";

    public Chapter Chapter { get; set; } = null!;
}
