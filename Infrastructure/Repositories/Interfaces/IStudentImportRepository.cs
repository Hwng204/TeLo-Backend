namespace Infrastructure.Repositories.Interface;

public sealed record ImportClassRow(ulong Id, string Code, string Name);

// Everything the validator needs from the database, fetched in bulk: the school's live classes for
// the target academic year and which of the file's codes already belong to a live student.
public sealed record ImportLookup(
    ulong SchoolId,
    ulong AcademicYearId,
    string AcademicYearName,
    IReadOnlyList<ImportClassRow> Classes,
    IReadOnlySet<string> ExistingCodes);

public sealed record NewImportRow(
    int RowNumber,
    string RawCode,
    string RawFullName,
    string RawDateOfBirth,
    string RawGender,
    string RawAdmissionDate,
    string RawClassCode,
    ulong? ResolvedSchoolClassId,
    bool IsValid,
    string? ErrorJson);

public sealed record NewImportBatch(
    ulong SchoolId,
    ulong AcademicYearId,
    string Source,
    ulong CreatedByUserId,
    string FileName,
    byte[] FileContent,
    DateTime CreatedAtUtc,
    IReadOnlyList<NewImportRow> Rows);

public sealed record ImportBatchSummary(
    ulong Id,
    ulong SchoolId,
    string SchoolName,
    ulong AcademicYearId,
    string AcademicYearName,
    string Source,
    string Status,
    string FileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    ulong CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewComment,
    DateTime? AppliedAt);

public sealed record ImportRowDetail(
    ulong RowId,
    int RowNumber,
    string RawCode,
    string RawFullName,
    string RawDateOfBirth,
    string RawGender,
    string RawAdmissionDate,
    string RawClassCode,
    ulong? ResolvedSchoolClassId,
    string? ResolvedClassName,
    bool IsValid,
    string? ErrorJson,
    ulong? CreatedStudentId);

public sealed record ImportBatchFile(string FileName, byte[] Content);

public sealed record ImportStudentToCreate(
    ulong RowId,
    int RowNumber,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly AdmissionDate,
    ulong SchoolClassId);

public enum ImportApplyStatus
{
    Success,

    // The batch was no longer in a state that can be applied (already applied, rejected, ...).
    StateInvalid,

    // Some rows stopped being valid between preview and apply; nothing was written.
    RowsConflict
}

public sealed record ImportRowConflict(int RowNumber, string Code, string Reason);

public sealed record ImportApplyResult(
    ImportApplyStatus Status,
    int Created,
    IReadOnlyList<ImportRowConflict> Conflicts);

// Storage for uploaded student-import files. School scope is enforced by the caller, which resolves
// the school (from the actor or from an admin route) and compares it with the batch.
public interface IStudentImportRepository
{
    // The school a school-side actor belongs to (users.school_branch_id -> school).
    Task<DirectoryReadResult<ulong>> GetActorSchoolIdAsync(
        ulong actorUserId,
        CancellationToken cancellationToken);

    // NotFound = the academic year id does not belong to this school's province.
    Task<DirectoryReadResult<ImportLookup>> GetLookupAsync(
        ulong schoolId,
        ulong? academicYearId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    Task<ulong> SaveDraftAsync(NewImportBatch batch, CancellationToken cancellationToken);

    Task<DirectoryRowsPage<ImportBatchSummary>> ListBatchesAsync(
        ulong? schoolId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ImportBatchSummary?> GetBatchAsync(ulong batchId, CancellationToken cancellationToken);

    Task<DirectoryRowsPage<ImportRowDetail>> GetRowsAsync(
        ulong batchId,
        int page,
        int pageSize,
        bool onlyInvalid,
        CancellationToken cancellationToken);

    // Valid rows that have not produced a student yet.
    Task<IReadOnlyList<ImportRowDetail>> GetPendingValidRowsAsync(
        ulong batchId,
        CancellationToken cancellationToken);

    Task<ImportBatchFile?> GetFileAsync(ulong batchId, CancellationToken cancellationToken);

    // Compare-and-set: moves the batch only if it is still in one of the expected statuses.
    // Returns false when another request got there first.
    Task<bool> TryTransitionAsync(
        ulong batchId,
        IReadOnlyCollection<string> fromStatuses,
        string toStatus,
        ulong? reviewerUserId,
        string? reviewComment,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    // One transaction: claim the batch (DRAFT/SUBMITTED -> APPLIED), re-check codes and classes,
    // insert the students with their first enrollment and stamp each row with its student.
    Task<ImportApplyResult> ApplyAsync(
        ulong batchId,
        ulong reviewerUserId,
        IReadOnlyList<ImportStudentToCreate> students,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
