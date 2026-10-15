namespace Domain.Entities.QuestionBank;

public sealed class ExamSetQuestion
{
    public ulong Id { get; set; }
    public ulong ExamSetId { get; set; }
    public ulong QuestionId { get; set; }
    public ExamSet ExamSet { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
