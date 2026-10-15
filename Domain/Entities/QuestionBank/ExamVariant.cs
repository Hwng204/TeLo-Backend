namespace Domain.Entities.QuestionBank;

public sealed class ExamVariant
{
    public ulong Id { get; set; }
    public ulong ExamSetId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    // JSON array of question ids in the order the variant shows them.
    public string QuestionOrder { get; set; } = "[]";
    public ExamSet ExamSet { get; set; } = null!;
}
