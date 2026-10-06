using Domain.Entities.Organization;

namespace Domain.Entities.Academic;

public sealed class AcademicContext
{
    public ulong Id { get; set; }
    public ulong AcademicYearId { get; set; }
    public ulong SchoolId { get; set; }
    public ulong SubjectId { get; set; }
    public ulong GradeLevelId { get; set; }
    public ulong SchoolBranchId { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;
    public School School { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public GradeLevel GradeLevel { get; set; } = null!;
    public SchoolBranch SchoolBranch { get; set; } = null!;
}
