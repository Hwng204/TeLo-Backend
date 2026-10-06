using Domain.Entities.Organization;

namespace Domain.Entities.Academic;

// Mỗi phân hiệu tự quản lý chương của mình; chương thuộc đúng một khối và một lĩnh vực.
public sealed class Chapter
{
    public ulong Id { get; set; }
    public ulong SchoolBranchId { get; set; }
    public ulong GradeLevelId { get; set; }
    public ulong FieldId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public uint SortOrder { get; set; }

    public SchoolBranch SchoolBranch { get; set; } = null!;
    public GradeLevel GradeLevel { get; set; } = null!;
    public SubjectField Field { get; set; } = null!;
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
