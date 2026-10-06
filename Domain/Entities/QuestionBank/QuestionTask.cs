using Domain.Entities.Academic;

namespace Domain.Entities.QuestionBank;

public sealed class QuestionTask
{
    public ulong Id { get; set; }
    public ulong TaskId { get; set; }
    public ulong LessonId { get; set; }
    public string Status { get; set; } = string.Empty;
    public uint AssignedQuestionCount { get; set; }

    public WorkTask Task { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public ICollection<QuestionTaskDetail> Details { get; set; } = new List<QuestionTaskDetail>();
}
