namespace Domain.Entities.Identity;

public static class StudentStatusCodes
{
    public const string Active = "ACTIVE";
    public const string TemporaryLeave = "TEMPORARY_LEAVE";
    public const string Transferred = "TRANSFERRED";

    // Logical delete: the profile stays for history and exam results but leaves active rosters.
    public const string Inactive = "INACTIVE";

    public static readonly string[] All = [Active, TemporaryLeave, Transferred, Inactive];
}

public static class StudentEnrollmentStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string TransferredOut = "TRANSFERRED_OUT";
}

public sealed class StudentEnrollment
{
    public ulong Id { get; set; }
    public ulong StudentId { get; set; }
    public ulong SchoolClassId { get; set; }
    public ulong AcademicYearId { get; set; }
    public string Status { get; set; } = StudentEnrollmentStatusCodes.Active;

    public Student Student { get; set; } = null!;
    public Organization.SchoolClass SchoolClass { get; set; } = null!;
    public Academic.AcademicYear AcademicYear { get; set; } = null!;
}
