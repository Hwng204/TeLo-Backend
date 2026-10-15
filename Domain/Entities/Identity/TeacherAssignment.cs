using Domain.Entities.Academic;
using Domain.Entities.Organization;

namespace Domain.Entities.Identity;

// A teacher teaching one subject in one class. Read by the directory's homeroom-only scope; nothing writes it yet.
public sealed class TeacherAssignment
{
    public ulong ClassId { get; set; }
    public ulong TeacherId { get; set; }
    public ulong SubjectId { get; set; }
    public DateTime AssignedAt { get; set; }

    public SchoolClass SchoolClass { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
