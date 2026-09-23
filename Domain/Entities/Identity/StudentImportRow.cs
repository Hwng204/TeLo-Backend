using Domain.Entities.Organization;

namespace Domain.Entities.Identity;

// One parsed sheet row. The Raw* columns keep exactly what the user typed so the preview shows
// their own text next to the error, not a normalised value.
public sealed class StudentImportRow
{
    public ulong Id { get; set; }
    public ulong BatchId { get; set; }
    public uint RowNumber { get; set; }
    public string RawCode { get; set; } = string.Empty;
    public string RawFullName { get; set; } = string.Empty;
    public string RawDateOfBirth { get; set; } = string.Empty;
    public string RawGender { get; set; } = string.Empty;
    public string RawAdmissionDate { get; set; } = string.Empty;
    public string RawClassCode { get; set; } = string.Empty;
    public ulong? ResolvedSchoolClassId { get; set; }
    public bool IsValid { get; set; }

    // JSON array of { field, message }; null when the row is valid.
    public string? ErrorJson { get; set; }

    // Set when the row has been turned into a student, so a retried apply never inserts twice.
    public ulong? CreatedStudentId { get; set; }

    public StudentImportBatch Batch { get; set; } = null!;
    public SchoolClass? ResolvedSchoolClass { get; set; }
    public Student? CreatedStudent { get; set; }
}
