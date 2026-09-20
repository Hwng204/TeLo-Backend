using Domain.Entities.Academic;
using Domain.Entities.Identity;

namespace Domain.Entities.Organization;

public sealed class SchoolClass
{
    public ulong Id { get; set; }
    public ulong SchoolBranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public ulong AcademicYearId { get; set; }
    public ulong GradeLevelId { get; set; }

    public SchoolBranch SchoolBranch { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public GradeLevel GradeLevel { get; set; } = null!;
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}

public static class SchoolClassStatusCodes
{
    public const string Active = "ACTIVE";

    // Logical delete: keeps the class in history, blocks new active enrollments.
    public const string Inactive = "INACTIVE";

    public static readonly string[] All = [Active, Inactive];
}
