namespace Domain.Entities.QuestionBank;

public sealed class QuestionPackageSubmission
{
    public ulong Id { get; set; }
    public ulong TaskId { get; set; }
    public int Number { get; set; }
    public int Version { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = "PENDING_REVIEW";
    public string SnapshotJson { get; set; } = "[]";
    public string NotesJson { get; set; } = "[]";
    public string? Comment { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public ulong? ReviewedByUserId { get; set; }
}
