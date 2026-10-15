using Domain.Entities.Identity;

namespace Domain.Entities.QuestionBank;

// Scope lives on the task's products: a matrix task's own matrix, an exam task's exam sets (their matrix).
public sealed class WorkTask
{
    public ulong Id { get; set; }
    public ulong CreatedByUserId { get; set; }
    public ulong AssignedToUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string TaskType { get; set; } = string.Empty;

    public User CreatedByUser { get; set; } = null!;
    public User AssignedToUser { get; set; } = null!;
}
