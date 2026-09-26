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

    // When this class period started and, once the student moves on, when it ended.
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }

    // Database-generated: the academic year while this row is ACTIVE, otherwise NULL. A unique
    // index over (StudentId, ActiveYearKey) lets one student hold many rows per year but only
    // one active one.
    public ulong? ActiveYearKey { get; private set; }

    public Student Student { get; set; } = null!;
    public Organization.SchoolClass SchoolClass { get; set; } = null!;
    public Academic.AcademicYear AcademicYear { get; set; } = null!;
}
