using Domain.Entities.Organization;

namespace Domain.Entities.Academic;

// Mỗi phân hiệu tự quản lý chương của mình; chương thuộc đúng một khối và một môn học.
public sealed class Chapter
{
    public ulong Id { get; set; }
    public ulong SchoolBranchId { get; set; }
    public ulong GradeLevelId { get; set; }
    public ulong SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";

    public SchoolBranch SchoolBranch { get; set; } = null!;
    public GradeLevel GradeLevel { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
