namespace Domain.Entities.QuestionBank;

public sealed class QuestionOption
{
    public ulong Id { get; set; }
    public ulong QuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string OptionKey { get; set; } = string.Empty;

    public Question Question { get; set; } = null!;
}
