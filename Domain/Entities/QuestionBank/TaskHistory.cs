namespace Domain.Entities.QuestionBank;

// One note on a task's timeline; today only a matrix rejection writes it (Content = the comment).
public sealed class TaskHistory
{
    public ulong Id { get; set; }
    public ulong TaskId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ulong CreateBy { get; set; }
    public DateTime CreateAt { get; set; }

    public WorkTask Task { get; set; } = null!;
}
