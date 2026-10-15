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

// API values only: ACTIVE = open row, COMPLETED = closed in a CLOSED year, otherwise TRANSFERRED_OUT.
public static class StudentEnrollmentStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string TransferredOut = "TRANSFERRED_OUT";
}

// Key (SchoolClassId, StudentId): a student can be in a class only once. Open = EndedOn null; the
// academic year comes from the class. Enrollment status is derived (see StudentEnrollmentStatusCodes).
public sealed class StudentEnrollment
{
    public ulong StudentId { get; set; }
    public ulong SchoolClassId { get; set; }

    // When this class period started and, once the student moves on, when it ended.
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }

    public Student Student { get; set; } = null!;
    public Organization.SchoolClass SchoolClass { get; set; } = null!;
}
