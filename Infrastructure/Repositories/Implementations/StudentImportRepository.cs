using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class StudentImportRepository(ApplicationDbContext db) : IStudentImportRepository
{
    private const int DuplicateKeyErrorNumber = 1062;
    private const string CodeTaken = "CODE_TAKEN";
    private const string ClassUnavailable = "CLASS_UNAVAILABLE";

    public async Task<DirectoryReadResult<ulong>> GetActorSchoolIdAsync(
        ulong actorUserId,
        CancellationToken cancellationToken)
    {
        var actor = await db.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new { SchoolId = (ulong?)u.SchoolBranch!.SchoolId })
            .SingleOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return DirectoryReadResult<ulong>.Fail(DirectoryReadStatus.ActorNotFound);
        }

        return actor.SchoolId is { } schoolId
            ? DirectoryReadResult<ulong>.Ok(schoolId)
            : DirectoryReadResult<ulong>.Fail(DirectoryReadStatus.SchoolScopeMissing);
    }

    public async Task<DirectoryReadResult<ImportLookup>> GetLookupAsync(
        ulong schoolId,
        ulong? academicYearId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var school = await db.Schools.AsNoTracking()
            .Where(s => s.Id == schoolId)
            .Select(s => new { s.ProvinceCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (school is null)
        {
            return DirectoryReadResult<ImportLookup>.Fail(DirectoryReadStatus.SchoolNotFound);
        }

        // Years are per province, so a school can only import into its own province's years.
        var yearQuery = db.AcademicYears.AsNoTracking()
            .Where(y => y.ProvinceCode == school.ProvinceCode);
        var year = academicYearId is { } requested
            ? await yearQuery.Where(y => y.Id == requested)
                .Select(y => new { y.Id, y.Name }).SingleOrDefaultAsync(cancellationToken)
            : await yearQuery.Where(y => y.Status == "ACTIVE")
                .OrderByDescending(y => y.StartDate)
                .Select(y => new { y.Id, y.Name }).FirstOrDefaultAsync(cancellationToken);
        if (year is null)
        {
            return DirectoryReadResult<ImportLookup>.Fail(
                academicYearId is null
                    ? DirectoryReadStatus.ActiveAcademicYearNotFound
                    : DirectoryReadStatus.NotFound);
        }

        var classes = await db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolBranch.SchoolId == schoolId &&
                c.AcademicYearId == year.Id &&
                c.Status == SchoolClassStatusCodes.Active)
            .OrderBy(c => c.GradeLevel.Name).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new ImportClassRow(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);

        var existing = codes.Count == 0
            ? new List<string?>()
            : await db.Students.AsNoTracking()
                .Where(s => s.ActiveCode != null && codes.Contains(s.ActiveCode))
                .Select(s => s.ActiveCode)
                .ToListAsync(cancellationToken);

        return DirectoryReadResult<ImportLookup>.Ok(new ImportLookup(
            schoolId, year.Id, year.Name, classes,
            existing.Where(c => c is not null).Select(c => c!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    public async Task<ulong> SaveDraftAsync(
        NewImportBatch batch,
        CancellationToken cancellationToken)
    {
        var valid = batch.Rows.Count(r => r.IsValid);
        var entity = new StudentImportBatch
        {
            SchoolId = batch.SchoolId,
            AcademicYearId = batch.AcademicYearId,
            Source = batch.Source,
            Status = StudentImportBatchStatusCodes.Draft,
            FileName = batch.FileName,
            FileContent = batch.FileContent,
            FileSize = (uint)batch.FileContent.Length,
            TotalRows = (uint)batch.Rows.Count,
            ValidRows = (uint)valid,
            InvalidRows = (uint)(batch.Rows.Count - valid),
            CreatedByUserId = batch.CreatedByUserId,
            CreatedAt = batch.CreatedAtUtc,
            Rows = batch.Rows.Select(r => new StudentImportRow
            {
                RowNumber = (uint)r.RowNumber,
                RawCode = r.RawCode,
                RawFullName = r.RawFullName,
                RawDateOfBirth = r.RawDateOfBirth,
                RawGender = r.RawGender,
                RawAdmissionDate = r.RawAdmissionDate,
                RawClassCode = r.RawClassCode,
                ResolvedSchoolClassId = r.ResolvedSchoolClassId,
                IsValid = r.IsValid,
                ErrorJson = r.ErrorJson
            }).ToList()
        };

        db.StudentImportBatches.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return entity.Id;
    }

    public async Task<DirectoryRowsPage<ImportBatchSummary>> ListBatchesAsync(
        ulong? schoolId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var batches = db.StudentImportBatches.AsNoTracking().AsQueryable();
        if (schoolId is { } school)
        {
            batches = batches.Where(b => b.SchoolId == school);
        }

        if (status is not null)
        {
            batches = batches.Where(b => b.Status == status);
        }

        var total = await batches.CountAsync(cancellationToken);
        var items = await Summaries(batches
                .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new DirectoryRowsPage<ImportBatchSummary>(items, total);
    }

    public Task<ImportBatchSummary?> GetBatchAsync(
        ulong batchId,
        CancellationToken cancellationToken) =>
        Summaries(db.StudentImportBatches.AsNoTracking().Where(b => b.Id == batchId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DirectoryRowsPage<ImportRowDetail>> GetRowsAsync(
        ulong batchId,
        int page,
        int pageSize,
        bool onlyInvalid,
        CancellationToken cancellationToken)
    {
        var rows = db.StudentImportRows.AsNoTracking().Where(r => r.BatchId == batchId);
        if (onlyInvalid)
        {
            rows = rows.Where(r => !r.IsValid);
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await Details(rows.OrderBy(r => r.RowNumber)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new DirectoryRowsPage<ImportRowDetail>(items, total);
    }

    public async Task<IReadOnlyList<ImportRowDetail>> GetPendingValidRowsAsync(
        ulong batchId,
        CancellationToken cancellationToken) =>
        await Details(db.StudentImportRows.AsNoTracking()
                .Where(r => r.BatchId == batchId && r.IsValid && r.CreatedStudentId == null)
                .OrderBy(r => r.RowNumber))
            .ToListAsync(cancellationToken);

    public Task<ImportBatchFile?> GetFileAsync(ulong batchId, CancellationToken cancellationToken) =>
        db.StudentImportBatches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new ImportBatchFile(b.FileName, b.FileContent))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryTransitionAsync(
        ulong batchId,
        IReadOnlyCollection<string> fromStatuses,
        string toStatus,
        ulong? reviewerUserId,
        string? reviewComment,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var claimed = db.StudentImportBatches
            .Where(b => b.Id == batchId && fromStatuses.Contains(b.Status));

        var updated = reviewerUserId is null
            ? await claimed.ExecuteUpdateAsync(
                s => s.SetProperty(b => b.Status, toStatus), cancellationToken)
            : await claimed.ExecuteUpdateAsync(
                s => s.SetProperty(b => b.Status, toStatus)
                    .SetProperty(b => b.ReviewedByUserId, reviewerUserId)
                    .SetProperty(b => b.ReviewedAt, nowUtc)
                    .SetProperty(b => b.ReviewComment, reviewComment),
                cancellationToken);
        return updated == 1;
    }

    public async Task<ImportApplyResult> ApplyAsync(
        ulong batchId,
        ulong reviewerUserId,
        IReadOnlyList<ImportStudentToCreate> students,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Claim first. Two concurrent applies race on this UPDATE; exactly one changes a row.
        var claimed = await db.StudentImportBatches
            .Where(b => b.Id == batchId &&
                ((b.Status == StudentImportBatchStatusCodes.Draft &&
                        b.Source == StudentImportSourceCodes.Admin) ||
                    b.Status == StudentImportBatchStatusCodes.Submitted))
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.Status, StudentImportBatchStatusCodes.Applied)
                    .SetProperty(b => b.AppliedAt, nowUtc)
                    .SetProperty(b => b.ReviewedByUserId, reviewerUserId)
                    .SetProperty(b => b.ReviewedAt, nowUtc),
                cancellationToken);
        if (claimed != 1)
        {
            return new ImportApplyResult(ImportApplyStatus.StateInvalid, 0, []);
        }

        var batch = await db.StudentImportBatches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new { b.SchoolId, b.AcademicYearId })
            .SingleAsync(cancellationToken);

        // Days can pass between preview and approval: codes may have been taken and classes
        // deactivated or moved since. Re-check before writing anything.
        var conflicts = await FindConflictsAsync(
            batch.SchoolId, batch.AcademicYearId, students, cancellationToken);
        if (conflicts.Count > 0)
        {
            return new ImportApplyResult(ImportApplyStatus.RowsConflict, 0, conflicts);
        }

        var rowIds = students.Select(s => s.RowId).ToArray();
        var rows = await db.StudentImportRows
            .Where(r => rowIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        foreach (var item in students)
        {
            var student = new Student
            {
                Code = item.Code,
                FullName = item.FullName,
                DateOfBirth = item.DateOfBirth,
                Gender = item.Gender,
                AdmissionDate = item.AdmissionDate,
                Status = StudentStatusCodes.Active,
                Enrollments =
                {
                    new StudentEnrollment
                    {
                        SchoolClassId = item.SchoolClassId,
                        AcademicYearId = batch.AcademicYearId,
                        Status = StudentEnrollmentStatusCodes.Active,
                        StartedOn = item.AdmissionDate
                    }
                }
            };
            db.Students.Add(student);
            // EF fills the FK once the student row is inserted, in the same SaveChanges.
            rows[item.RowId].CreatedStudent = student;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: DuplicateKeyErrorNumber })
        {
            // A concurrent manual create won the race for one of the codes after our check.
            db.ChangeTracker.Clear();
            return new ImportApplyResult(
                ImportApplyStatus.RowsConflict, 0,
                [new ImportRowConflict(0, string.Empty, CodeTaken)]);
        }

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new ImportApplyResult(ImportApplyStatus.Success, students.Count, []);
    }

    private async Task<List<ImportRowConflict>> FindConflictsAsync(
        ulong schoolId,
        ulong academicYearId,
        IReadOnlyList<ImportStudentToCreate> students,
        CancellationToken cancellationToken)
    {
        var conflicts = new List<ImportRowConflict>();
        var codes = students.Select(s => s.Code).ToArray();
        var taken = (await db.Students.AsNoTracking()
                .Where(s => s.ActiveCode != null && codes.Contains(s.ActiveCode))
                .Select(s => s.ActiveCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var classIds = students.Select(s => s.SchoolClassId).Distinct().ToArray();
        var usable = (await db.SchoolClasses.AsNoTracking()
                .Where(c => classIds.Contains(c.Id) &&
                    c.SchoolBranch.SchoolId == schoolId &&
                    c.AcademicYearId == academicYearId &&
                    c.Status == SchoolClassStatusCodes.Active)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var student in students)
        {
            if (taken.Contains(student.Code))
            {
                conflicts.Add(new ImportRowConflict(student.RowNumber, student.Code, CodeTaken));
            }
            else if (!usable.Contains(student.SchoolClassId))
            {
                conflicts.Add(new ImportRowConflict(student.RowNumber, student.Code, ClassUnavailable));
            }
        }

        return conflicts;
    }

    private static IQueryable<ImportBatchSummary> Summaries(IQueryable<StudentImportBatch> batches) =>
        batches.Select(b => new ImportBatchSummary(
            b.Id, b.SchoolId, b.School.Name, b.AcademicYearId, b.AcademicYear.Name,
            b.Source, b.Status, b.FileName,
            (int)b.TotalRows, (int)b.ValidRows, (int)b.InvalidRows,
            b.CreatedByUserId, b.CreatedByUser.FullName, b.CreatedAt,
            b.ReviewedByUser == null ? null : b.ReviewedByUser.FullName,
            b.ReviewedAt, b.ReviewComment, b.AppliedAt));

    private static IQueryable<ImportRowDetail> Details(IQueryable<StudentImportRow> rows) =>
        rows.Select(r => new ImportRowDetail(
            r.Id, (int)r.RowNumber, r.RawCode, r.RawFullName, r.RawDateOfBirth, r.RawGender,
            r.RawAdmissionDate, r.RawClassCode, r.ResolvedSchoolClassId,
            r.ResolvedSchoolClass == null ? null : r.ResolvedSchoolClass.Name,
            r.IsValid, r.ErrorJson, r.CreatedStudentId));
}
