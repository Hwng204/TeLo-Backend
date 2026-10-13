using Domain.Entities.Identity;

namespace Domain.Entities.QuestionBank;

public sealed class Question
{
    public DateTime? PracticeAt { get; set; }
    public ulong? PracticeByUserId { get; set; }
    public string? PracticeReason { get; set; }
    public ulong Id { get; set; }
    public ulong QuestionTaskDetailId { get; set; }
    public ulong? QuestionBankId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
    public string Status { get; set; } = string.Empty;
    public ulong CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public ulong? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public QuestionTaskDetail QuestionTaskDetail { get; set; } = null!;
    public QuestionBank? QuestionBank { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
