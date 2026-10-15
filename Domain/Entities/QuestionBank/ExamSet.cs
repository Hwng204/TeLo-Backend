using Domain.Entities.Identity;

namespace Domain.Entities.QuestionBank;

public static class ExamSetStatusCodes
{
    // An exam task creates its papers as empty DRAFT sets.
    public const string Draft = "DRAFT";
}

public sealed class ExamSet
{
    public ulong Id { get; set; }
    public ulong ExamMatrixId { get; set; }
    public ulong? TaskId { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint DurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    // Column review_by_user_id: the person assigned to review the set.
    public ulong? ApprovedByUserId { get; set; }
    public ExamMatrix ExamMatrix { get; set; } = null!;
    public WorkTask? Task { get; set; }
    public User? ApprovedByUser { get; set; }
    public ICollection<ExamSetQuestion> Questions { get; set; } = new List<ExamSetQuestion>();
    public ICollection<ExamVariant> Variants { get; set; } = new List<ExamVariant>();
}
