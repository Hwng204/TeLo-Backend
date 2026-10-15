using Domain.Entities.Academic;
using Domain.Entities.Identity;

namespace Domain.Entities.QuestionBank;

public sealed class Question
{
    public ulong Id { get; set; }
    public ulong QuestionTaskDetailId { get; set; }
    public ulong LessonId { get; set; }
    public string BankType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AnswerExplanation { get; set; }
    public string Status { get; set; } = string.Empty;
    public ulong CreatedByUserId { get; set; }

    public QuestionTaskDetail QuestionTaskDetail { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
