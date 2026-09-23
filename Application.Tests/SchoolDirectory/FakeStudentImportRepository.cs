using Domain.Entities.Identity;
using Infrastructure.Repositories.Interface;

namespace Application.Tests.SchoolDirectory;

// In-memory stand-in that keeps real batch state, so lifecycle rules (compare-and-set, school
// isolation, terminal states) are exercised the way the database would enforce them.
internal sealed class FakeStudentImportRepository : IStudentImportRepository
{
    public const ulong SchoolA = 1;
    public const ulong SchoolB = 2;
    public const ulong YearId = 10;

    private sealed class Stored
    {
        public required ulong Id { get; init; }
        public required NewImportBatch Batch { get; init; }
        public required List<ImportRowDetail> Rows { get; init; }
        public string Status { get; set; } = StudentImportBatchStatusCodes.Draft;
        public ulong? ReviewerId { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? AppliedAt { get; set; }
    }

    private readonly Dictionary<ulong, Stored> batches = new();
    private ulong nextId = 100;

    // Actor id -> school; an id that is absent is unknown, 999 has no branch.
    public Dictionary<ulong, ulong> ActorSchools { get; } = new() { [5] = SchoolA, [6] = SchoolB };

    public IReadOnlyList<ImportClassRow> Classes { get; set; } =
    [
        new(101, "6A", "Lớp 6A"),
        new(102, "6B", "Lớp 6B")
    ];

    public HashSet<string> ExistingCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    // When set, the next ApplyAsync reports these conflicts and writes nothing.
    public List<ImportRowConflict>? ApplyConflicts { get; set; }

    public int ApplyCalls { get; private set; }

    public IReadOnlyList<ImportStudentToCreate>? LastApplied { get; private set; }

    public int SavedBatches => batches.Count;

    public Task<DirectoryReadResult<ulong>> GetActorSchoolIdAsync(
        ulong actorUserId, CancellationToken cancellationToken)
    {
        if (actorUserId == 999)
        {
            return Task.FromResult(DirectoryReadResult<ulong>.Fail(DirectoryReadStatus.SchoolScopeMissing));
        }

        return Task.FromResult(ActorSchools.TryGetValue(actorUserId, out var school)
            ? DirectoryReadResult<ulong>.Ok(school)
            : DirectoryReadResult<ulong>.Fail(DirectoryReadStatus.ActorNotFound));
    }

    public Task<DirectoryReadResult<ImportLookup>> GetLookupAsync(
        ulong schoolId, ulong? academicYearId, IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (schoolId is not (SchoolA or SchoolB))
        {
            return Task.FromResult(DirectoryReadResult<ImportLookup>.Fail(DirectoryReadStatus.SchoolNotFound));
        }

        if (academicYearId is { } year && year != YearId)
        {
            return Task.FromResult(DirectoryReadResult<ImportLookup>.Fail(DirectoryReadStatus.NotFound));
        }

        var existing = ExistingCodes.Where(c => codes.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(DirectoryReadResult<ImportLookup>.Ok(
            new ImportLookup(schoolId, YearId, "2025-2026", Classes, existing)));
    }

    public Task<ulong> SaveDraftAsync(NewImportBatch batch, CancellationToken cancellationToken)
    {
        var id = nextId++;
        batches[id] = new Stored
        {
            Id = id,
            Batch = batch,
            Rows = batch.Rows.Select((r, index) => new ImportRowDetail(
                (ulong)(id * 1000 + (ulong)index), r.RowNumber, r.RawCode, r.RawFullName,
                r.RawDateOfBirth, r.RawGender, r.RawAdmissionDate, r.RawClassCode,
                r.ResolvedSchoolClassId, null, r.IsValid, r.ErrorJson, null)).ToList()
        };
        return Task.FromResult(id);
    }

    public Task<DirectoryRowsPage<ImportBatchSummary>> ListBatchesAsync(
        ulong? schoolId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = batches.Values
            .Where(b => schoolId is null || b.Batch.SchoolId == schoolId)
            .Where(b => status is null || b.Status == status)
            .OrderByDescending(b => b.Id)
            .Select(Summary).ToList();
        return Task.FromResult(new DirectoryRowsPage<ImportBatchSummary>(
            items.Skip((page - 1) * pageSize).Take(pageSize).ToList(), items.Count));
    }

    public Task<ImportBatchSummary?> GetBatchAsync(ulong batchId, CancellationToken cancellationToken) =>
        Task.FromResult(batches.TryGetValue(batchId, out var stored) ? Summary(stored) : null);

    public Task<DirectoryRowsPage<ImportRowDetail>> GetRowsAsync(
        ulong batchId, int page, int pageSize, bool onlyInvalid, CancellationToken cancellationToken)
    {
        var rows = batches[batchId].Rows.Where(r => !onlyInvalid || !r.IsValid).ToList();
        return Task.FromResult(new DirectoryRowsPage<ImportRowDetail>(
            rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), rows.Count));
    }

    public Task<IReadOnlyList<ImportRowDetail>> GetPendingValidRowsAsync(
        ulong batchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImportRowDetail>>(
            batches[batchId].Rows.Where(r => r.IsValid && r.CreatedStudentId is null).ToList());

    public Task<ImportBatchFile?> GetFileAsync(ulong batchId, CancellationToken cancellationToken) =>
        Task.FromResult(batches.TryGetValue(batchId, out var stored)
            ? new ImportBatchFile(stored.Batch.FileName, stored.Batch.FileContent)
            : null);

    public Task<bool> TryTransitionAsync(
        ulong batchId, IReadOnlyCollection<string> fromStatuses, string toStatus,
        ulong? reviewerUserId, string? reviewComment, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var stored = batches[batchId];
        if (!fromStatuses.Contains(stored.Status))
        {
            return Task.FromResult(false);
        }

        stored.Status = toStatus;
        if (reviewerUserId is not null)
        {
            stored.ReviewerId = reviewerUserId;
            stored.ReviewComment = reviewComment;
        }

        return Task.FromResult(true);
    }

    public Task<ImportApplyResult> ApplyAsync(
        ulong batchId, ulong reviewerUserId, IReadOnlyList<ImportStudentToCreate> students,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        ApplyCalls++;
        var stored = batches[batchId];
        var applicable =
            (stored.Status == StudentImportBatchStatusCodes.Draft &&
                stored.Batch.Source == StudentImportSourceCodes.Admin) ||
            stored.Status == StudentImportBatchStatusCodes.Submitted;
        if (!applicable)
        {
            return Task.FromResult(new ImportApplyResult(ImportApplyStatus.StateInvalid, 0, []));
        }

        if (ApplyConflicts is { } conflicts)
        {
            // Mirrors the transaction rollback: the claim is undone with everything else.
            return Task.FromResult(new ImportApplyResult(ImportApplyStatus.RowsConflict, 0, conflicts));
        }

        stored.Status = StudentImportBatchStatusCodes.Applied;
        stored.ReviewerId = reviewerUserId;
        stored.AppliedAt = nowUtc;
        LastApplied = students;
        for (var i = 0; i < stored.Rows.Count; i++)
        {
            var match = students.FirstOrDefault(s => s.RowId == stored.Rows[i].RowId);
            if (match is not null)
            {
                stored.Rows[i] = stored.Rows[i] with { CreatedStudentId = 5000 + (ulong)i };
            }
        }

        return Task.FromResult(new ImportApplyResult(ImportApplyStatus.Success, students.Count, []));
    }

    public string StatusOf(ulong batchId) => batches[batchId].Status;

    public ulong? ReviewerOf(ulong batchId) => batches[batchId].ReviewerId;

    public string? CommentOf(ulong batchId) => batches[batchId].ReviewComment;

    private static ImportBatchSummary Summary(Stored s) => new(
        s.Id, s.Batch.SchoolId, $"Trường {s.Batch.SchoolId}", s.Batch.AcademicYearId, "2025-2026",
        s.Batch.Source, s.Status, s.Batch.FileName, s.Rows.Count, s.Rows.Count(r => r.IsValid),
        s.Rows.Count(r => !r.IsValid), s.Batch.CreatedByUserId, "Người tải", s.Batch.CreatedAtUtc,
        s.ReviewerId is null ? null : "Admin", null, s.ReviewComment, s.AppliedAt);
}
