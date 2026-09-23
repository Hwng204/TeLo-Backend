using Domain.Entities.Academic;
using Domain.Entities.Organization;

namespace Domain.Entities.Identity;

public static class StudentImportBatchStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string Rejected = "REJECTED";
    public const string Applied = "APPLIED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = [Draft, Submitted, Rejected, Applied, Cancelled];
}

public static class StudentImportSourceCodes
{
    // Uploaded by a system admin, applied directly.
    public const string Admin = "ADMIN";

    // Uploaded by a school principal/vice principal, reviewed by an admin before it applies.
    public const string School = "SCHOOL";

    public static readonly string[] All = [Admin, School];
}

// One uploaded Excel file. Keeps the original bytes next to the parsed rows so a reviewer can
// read the table in the browser and still download exactly what the school sent.
public sealed class StudentImportBatch
{
    public ulong Id { get; set; }
    public ulong SchoolId { get; set; }
    public ulong AcademicYearId { get; set; }
    public string Source { get; set; } = StudentImportSourceCodes.Admin;
    public string Status { get; set; } = StudentImportBatchStatusCodes.Draft;
    public string FileName { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = [];
    public uint FileSize { get; set; }
    public uint TotalRows { get; set; }
    public uint ValidRows { get; set; }
    public uint InvalidRows { get; set; }
    public ulong CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? AppliedAt { get; set; }

    public School School { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public ICollection<StudentImportRow> Rows { get; set; } = new List<StudentImportRow>();
}
