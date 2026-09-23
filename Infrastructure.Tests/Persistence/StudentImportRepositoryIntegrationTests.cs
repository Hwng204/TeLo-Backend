using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

// Student import storage and apply. Runs against ConnectionStrings__SchoolDirectoryTest with every
// migration applied. Fixture ids live in 94000-94999 (province "94"); students created by apply get
// server-generated ids, so they are recognised by their "IMP94-" code prefix. Everything is deleted
// after each test.
[Collection(SchoolDirectoryDatabaseCollection.Name)]
public sealed class StudentImportRepositoryIntegrationTests
{
    private const ulong School = 94001;
    private const ulong OtherSchool = 94002;
    private const ulong Branch = 94011;
    private const ulong OtherBranch = 94012;
    private const ulong Year = 94021;
    private const ulong ForeignYear = 94022;
    private const ulong Grade = 94041;
    private const ulong Class6A = 94051;
    private const ulong Class6B = 94052;
    private const ulong InactiveClass = 94053;
    private const ulong ForeignClass = 94054;
    private const ulong Creator = 94061;
    private const ulong Reviewer = 94062;
    private const ulong Homeless = 94063;
    private const string Province = "94";

    [Fact]
    public async Task ActorSchool_IsResolvedFromTheBranchAndRefusesMissingScope()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);

            var ok = await repository.GetActorSchoolIdAsync(Creator, CancellationToken.None);
            var noBranch = await repository.GetActorSchoolIdAsync(Homeless, CancellationToken.None);
            var unknown = await repository.GetActorSchoolIdAsync(94999, CancellationToken.None);

            Assert.Equal(School, ok.Value);
            Assert.Equal(DirectoryReadStatus.SchoolScopeMissing, noBranch.Status);
            Assert.Equal(DirectoryReadStatus.ActorNotFound, unknown.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Lookup_ReturnsOnlyLiveClassesOfThisSchoolAndTakenCodesOfLiveStudents()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);

            var result = await repository.GetLookupAsync(
                School, null, ["IMP94-LIVE", "IMP94-GONE", "IMP94-FREE"], CancellationToken.None);

            var lookup = result.Value!;
            Assert.Equal(Year, lookup.AcademicYearId);
            Assert.Equal(new[] { Class6A, Class6B }, lookup.Classes.Select(c => c.Id));
            // A deleted student no longer owns its code.
            Assert.Equal(new[] { "IMP94-LIVE" }, lookup.ExistingCodes.Order());
            Assert.Contains("imp94-live", lookup.ExistingCodes);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Lookup_RejectsUnknownSchoolsAndYearsOutsideTheSchoolsProvince()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);

            var unknownSchool = await repository.GetLookupAsync(
                94999, null, [], CancellationToken.None);
            var foreignYear = await repository.GetLookupAsync(
                School, ForeignYear, [], CancellationToken.None);
            var explicitYear = await repository.GetLookupAsync(
                School, Year, [], CancellationToken.None);

            Assert.Equal(DirectoryReadStatus.SchoolNotFound, unknownSchool.Status);
            Assert.Equal(DirectoryReadStatus.NotFound, foreignYear.Status);
            Assert.Equal(Year, explicitYear.Value!.AcademicYearId);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Draft_RoundTripsRowsCountsAndAMegabyteOfFileContent()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var content = new byte[1024 * 1024];
            new Random(42).NextBytes(content);

            var id = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.Admin, content,
                    Row(2, "IMP94-A", Class6A), Row(3, "", null, valid: false, errorJson: "[{\"field\":\"code\",\"message\":\"Thiếu\"}]")),
                CancellationToken.None);

            var batch = await repository.GetBatchAsync(id, CancellationToken.None);
            var rows = await repository.GetRowsAsync(id, 1, 10, false, CancellationToken.None);
            var invalid = await repository.GetRowsAsync(id, 1, 10, true, CancellationToken.None);
            var file = await repository.GetFileAsync(id, CancellationToken.None);

            Assert.Equal((2, 1, 1), (batch!.TotalRows, batch.ValidRows, batch.InvalidRows));
            Assert.Equal(StudentImportBatchStatusCodes.Draft, batch.Status);
            Assert.Equal("Truong Import A", batch.SchoolName);
            Assert.Equal(new[] { 2, 3 }, rows.Items.Select(r => r.RowNumber));
            Assert.Equal("Lớp 6A", rows.Items[0].ResolvedClassName);
            Assert.Equal(3, rows.Items[1].RowNumber);
            Assert.Equal(1, invalid.TotalCount);
            Assert.Contains("Thiếu", invalid.Items[0].ErrorJson);
            Assert.Equal(content, file!.Content);
            Assert.Equal("ds.xlsx", file.FileName);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ListBatches_FiltersBySchoolAndStatusNewestFirst()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var first = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.Admin, [1], Row(2, "IMP94-A", Class6A)),
                CancellationToken.None);
            var second = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.School, [1], Row(2, "IMP94-B", Class6A)),
                CancellationToken.None);
            await repository.TryTransitionAsync(
                second, [StudentImportBatchStatusCodes.Draft],
                StudentImportBatchStatusCodes.Submitted, null, null, DateTime.UtcNow,
                CancellationToken.None);
            var other = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.School, [1], Row(2, "IMP94-C", ForeignClass)) with
                {
                    SchoolId = OtherSchool
                },
                CancellationToken.None);

            var all = await repository.ListBatchesAsync(null, null, 1, 20, CancellationToken.None);
            var mine = await repository.ListBatchesAsync(School, null, 1, 20, CancellationToken.None);
            var submitted = await repository.ListBatchesAsync(
                null, StudentImportBatchStatusCodes.Submitted, 1, 20, CancellationToken.None);

            Assert.Equal(3, all.TotalCount);
            Assert.Equal(new[] { second, first }, mine.Items.Select(b => b.Id));
            Assert.Equal(new[] { second }, submitted.Items.Select(b => b.Id));
            Assert.Contains(other, all.Items.Select(b => b.Id));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transition_MovesABatchOnlyFromAnExpectedStatusAndRecordsTheReview()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.School, [1], Row(2, "IMP94-A", Class6A)),
                CancellationToken.None);
            var when = new DateTime(2025, 10, 1, 8, 0, 0);

            var wrongSource = await repository.TryTransitionAsync(
                id, [StudentImportBatchStatusCodes.Submitted],
                StudentImportBatchStatusCodes.Rejected, Reviewer, "no", when, CancellationToken.None);
            var submit = await repository.TryTransitionAsync(
                id, [StudentImportBatchStatusCodes.Draft],
                StudentImportBatchStatusCodes.Submitted, null, null, when, CancellationToken.None);
            var reject = await repository.TryTransitionAsync(
                id, [StudentImportBatchStatusCodes.Submitted],
                StudentImportBatchStatusCodes.Rejected, Reviewer, "Sai mã lớp", when,
                CancellationToken.None);
            var again = await repository.TryTransitionAsync(
                id, [StudentImportBatchStatusCodes.Submitted],
                StudentImportBatchStatusCodes.Rejected, Reviewer, "lần hai", when,
                CancellationToken.None);

            var batch = await repository.GetBatchAsync(id, CancellationToken.None);
            Assert.False(wrongSource);
            Assert.True(submit);
            Assert.True(reject);
            Assert.False(again);
            Assert.Equal(StudentImportBatchStatusCodes.Rejected, batch!.Status);
            Assert.Equal("Sai mã lớp", batch.ReviewComment);
            Assert.Equal("Nguoi duyet", batch.ReviewedByName);
            Assert.Equal(when, batch.ReviewedAt);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_CreatesStudentsWithFirstEnrollmentAndStampsEveryRow()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await SaveAdminBatch(repository, "IMP94-A", "IMP94-B");
            var pending = await repository.GetPendingValidRowsAsync(id, CancellationToken.None);

            var result = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(ImportApplyStatus.Success, result.Status);
            Assert.Equal(2, result.Created);
            var students = await context.Students.AsNoTracking()
                .Include(s => s.Enrollments).Where(s => s.Code.StartsWith("IMP94-A") || s.Code.StartsWith("IMP94-B"))
                .OrderBy(s => s.Code).ToListAsync();
            Assert.Equal(2, students.Count);
            Assert.All(students, s => Assert.Equal(StudentStatusCodes.Active, s.Status));
            var enrollment = Assert.Single(students[0].Enrollments);
            Assert.Equal(
                (Class6A, Year, StudentEnrollmentStatusCodes.Active, new DateOnly(2025, 9, 5)),
                (enrollment.SchoolClassId, enrollment.AcademicYearId, enrollment.Status, enrollment.StartedOn));

            var batch = await repository.GetBatchAsync(id, CancellationToken.None);
            Assert.Equal(StudentImportBatchStatusCodes.Applied, batch!.Status);
            Assert.NotNull(batch.AppliedAt);
            Assert.Equal("Nguoi duyet", batch.ReviewedByName);
            var rows = await repository.GetRowsAsync(id, 1, 10, false, CancellationToken.None);
            Assert.Equal(
                students.Select(s => s.Id).Order(),
                rows.Items.Select(r => r.CreatedStudentId!.Value).Order());
            Assert.Empty(await repository.GetPendingValidRowsAsync(id, CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_TwiceCreatesNothingTheSecondTime()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await SaveAdminBatch(repository, "IMP94-A");
            var pending = await repository.GetPendingValidRowsAsync(id, CancellationToken.None);

            var first = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);
            var second = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(ImportApplyStatus.Success, first.Status);
            Assert.Equal(ImportApplyStatus.StateInvalid, second.Status);
            Assert.Equal(1, await context.Students.CountAsync(s => s.Code == "IMP94-A"));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_RunInParallelLetsExactlyOneRequestWrite()
    {
        await using var context = await SeedAsync();
        try
        {
            var seeding = new StudentImportRepository(context);
            var id = await SaveAdminBatch(seeding, "IMP94-A", "IMP94-B", "IMP94-C");
            var pending = await seeding.GetPendingValidRowsAsync(id, CancellationToken.None);
            var students = ToStudents(pending);

            // Two requests, two contexts (as two HTTP requests would have), same batch.
            await using var first = CreateContext();
            await using var second = CreateContext();
            var results = await Task.WhenAll(
                new StudentImportRepository(first).ApplyAsync(
                    id, Reviewer, students, DateTime.UtcNow, CancellationToken.None),
                new StudentImportRepository(second).ApplyAsync(
                    id, Reviewer, students, DateTime.UtcNow, CancellationToken.None));

            Assert.Equal(1, results.Count(r => r.Status == ImportApplyStatus.Success));
            Assert.Equal(1, results.Count(r => r.Status == ImportApplyStatus.StateInvalid));
            Assert.Equal(3, await context.Students.CountAsync(s => s.Code.StartsWith("IMP94-")
                && s.Code != "IMP94-LIVE" && s.Code != "IMP94-GONE"));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_ANewLiveStudentTookACodeAfterPreview_WritesNothingAndKeepsTheBatchOpen()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await SaveAdminBatch(repository, "IMP94-A", "IMP94-B");
            var pending = await repository.GetPendingValidRowsAsync(id, CancellationToken.None);
            // Someone creates the same code by hand between preview and apply.
            context.Students.Add(new Student
            {
                Code = "IMP94-B", FullName = "Nhanh tay", AdmissionDate = new DateOnly(2025, 9, 1)
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var result = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(ImportApplyStatus.RowsConflict, result.Status);
            var conflict = Assert.Single(result.Conflicts);
            Assert.Equal(("IMP94-B", "CODE_TAKEN"), (conflict.Code, conflict.Reason));
            // The whole batch rolled back: not even the valid first student exists, and the
            // batch can still be applied once the clash is resolved.
            Assert.Equal(0, await context.Students.CountAsync(s => s.Code == "IMP94-A"));
            var batch = await repository.GetBatchAsync(id, CancellationToken.None);
            Assert.Equal(StudentImportBatchStatusCodes.Draft, batch!.Status);
            Assert.Null(batch.AppliedAt);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_AClassDeactivatedAfterPreviewIsReportedAsUnavailable()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await SaveAdminBatch(repository, "IMP94-A", "IMP94-B");
            var pending = await repository.GetPendingValidRowsAsync(id, CancellationToken.None);
            await context.SchoolClasses.Where(c => c.Id == Class6B)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, SchoolClassStatusCodes.Inactive));

            var result = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(ImportApplyStatus.RowsConflict, result.Status);
            var conflict = Assert.Single(result.Conflicts);
            Assert.Equal(("IMP94-B", "CLASS_UNAVAILABLE"), (conflict.Code, conflict.Reason));
            Assert.Equal(0, await context.Students.CountAsync(s => s.Code == "IMP94-A"));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Apply_MayReuseTheCodeOfADeletedStudentAndSubmittedSchoolBatchesApply()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new StudentImportRepository(context);
            var id = await repository.SaveDraftAsync(
                Batch(StudentImportSourceCodes.School, [1], Row(2, "IMP94-GONE", Class6A)),
                CancellationToken.None);
            var pending = await repository.GetPendingValidRowsAsync(id, CancellationToken.None);

            var beforeSubmit = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);
            await repository.TryTransitionAsync(
                id, [StudentImportBatchStatusCodes.Draft], StudentImportBatchStatusCodes.Submitted,
                null, null, DateTime.UtcNow, CancellationToken.None);
            var afterSubmit = await repository.ApplyAsync(
                id, Reviewer, ToStudents(pending), DateTime.UtcNow, CancellationToken.None);

            // A school draft is not applicable until it has been submitted for review.
            Assert.Equal(ImportApplyStatus.StateInvalid, beforeSubmit.Status);
            Assert.Equal(ImportApplyStatus.Success, afterSubmit.Status);
            var codes = await context.Students.AsNoTracking()
                .Where(s => s.Code == "IMP94-GONE").Select(s => s.Status).OrderBy(s => s).ToListAsync();
            Assert.Equal(
                new[] { StudentStatusCodes.Active, StudentStatusCodes.Inactive }, codes);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static async Task<ulong> SaveAdminBatch(
        StudentImportRepository repository, params string[] codes) =>
        await repository.SaveDraftAsync(
            Batch(StudentImportSourceCodes.Admin, [1, 2, 3],
                codes.Select((code, index) =>
                    Row(index + 2, code, index == 1 ? Class6B : Class6A)).ToArray()),
            CancellationToken.None);

    private static List<ImportStudentToCreate> ToStudents(IReadOnlyList<ImportRowDetail> rows) =>
        rows.Select(r => new ImportStudentToCreate(
            r.RowId, r.RowNumber, r.RawCode, r.RawFullName, null, "NAM",
            new DateOnly(2025, 9, 5), r.ResolvedSchoolClassId!.Value)).ToList();

    private static NewImportBatch Batch(string source, byte[] content, params NewImportRow[] rows) => new(
        School, Year, source, Creator, "ds.xlsx", content, DateTime.UtcNow, rows);

    private static NewImportRow Row(
        int number, string code, ulong? classId, bool valid = true, string? errorJson = null) => new(
        number, code, $"Hoc sinh {code}", "", "NAM", "05/09/2025", "6A", classId, valid, errorJson);

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var context = CreateContext();
        await CleanupAsync(context);

        context.Provinces.Add(new Province { Code = Province, Name = "Tinh Import Test" });
        var year = new AcademicYear
        {
            Id = Year, Name = "Import 2025-2026", Status = "ACTIVE", ProvinceCode = Province,
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 6, 30)
        };
        year.AssignCode("T94021");
        // No province: not a year this school may import into.
        var foreignYear = new AcademicYear
        {
            Id = ForeignYear, Name = "Import Foreign", Status = "DRAFT",
            StartDate = new DateOnly(2030, 9, 1), EndDate = new DateOnly(2031, 6, 30)
        };
        foreignYear.AssignCode("T94022");
        context.AcademicYears.AddRange(year, foreignYear);
        context.GradeLevels.Add(new GradeLevel { Id = Grade, Name = "Import Khoi 6" });
        context.Schools.AddRange(
            new School { Id = School, Code = "T94001", Name = "Truong Import A", ProvinceCode = Province },
            new School { Id = OtherSchool, Code = "T94002", Name = "Truong Import B", ProvinceCode = Province });
        context.SchoolBranches.AddRange(
            new SchoolBranch { Id = Branch, SchoolId = School, Code = "T94011", Name = "Co so A" },
            new SchoolBranch { Id = OtherBranch, SchoolId = OtherSchool, Code = "T94012", Name = "Co so B" });
        context.Users.AddRange(
            User(Creator, Branch, "Nguoi tai"), User(Reviewer, null, "Nguoi duyet"),
            User(Homeless, null, "Khong co truong"));
        await context.SaveChangesAsync();

        context.SchoolClasses.AddRange(
            Class(Class6A, Branch, "I6A", "Lớp 6A"), Class(Class6B, Branch, "I6B", "Lớp 6B"),
            Class(InactiveClass, Branch, "I6X", "Lớp 6X", SchoolClassStatusCodes.Inactive),
            Class(ForeignClass, OtherBranch, "I6F", "Lớp 6F"));
        context.Students.AddRange(
            new Student
            {
                Code = "IMP94-LIVE", FullName = "Dang hoc", AdmissionDate = new DateOnly(2024, 9, 1)
            },
            new Student
            {
                Code = "IMP94-GONE", FullName = "Da xoa", AdmissionDate = new DateOnly(2024, 9, 1),
                Status = StudentStatusCodes.Inactive
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    private static User User(ulong id, ulong? branchId, string name) => new()
    {
        Id = id, Username = $"t{id}", Email = $"t{id}@test.local", SchoolBranchId = branchId,
        PasswordHash = "x", FullName = name
    };

    private static SchoolClass Class(
        ulong id, ulong branchId, string code, string name,
        string status = SchoolClassStatusCodes.Active) => new()
    {
        Id = id, SchoolBranchId = branchId, Code = code, Name = name, Status = status,
        AcademicYearId = Year, GradeLevelId = Grade
    };

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        context.ChangeTracker.Clear();
        // Rows go with their batches (cascade); batches must go before users, classes and students.
        await context.StudentImportBatches
            .Where(b => b.SchoolId >= 94000 && b.SchoolId < 95000).ExecuteDeleteAsync();
        var fixtureClasses = context.SchoolClasses
            .Where(c => c.SchoolBranchId >= 94000 && c.SchoolBranchId < 95000)
            .Select(c => c.Id);
        await context.StudentEnrollments
            .Where(e => fixtureClasses.Contains(e.SchoolClassId)).ExecuteDeleteAsync();
        await context.Students.Where(s => s.Code.StartsWith("IMP94-")).ExecuteDeleteAsync();
        await context.SchoolClasses
            .Where(c => c.SchoolBranchId >= 94000 && c.SchoolBranchId < 95000).ExecuteDeleteAsync();
        await context.Users.Where(u => u.Id >= 94000 && u.Id < 95000).ExecuteDeleteAsync();
        await context.AcademicYears.Where(y => y.Id >= 94000 && y.Id < 95000).ExecuteDeleteAsync();
        await context.GradeLevels.Where(g => g.Id >= 94000 && g.Id < 95000).ExecuteDeleteAsync();
        await context.SchoolBranches.Where(b => b.Id >= 94000 && b.Id < 95000).ExecuteDeleteAsync();
        await context.Schools.Where(s => s.Id >= 94000 && s.Id < 95000).ExecuteDeleteAsync();
        await context.Provinces.Where(p => p.Code == Province).ExecuteDeleteAsync();
    }

    private static ApplicationDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__SchoolDirectoryTest");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__SchoolDirectoryTest must be configured for school directory integration tests.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ApplicationDbContext(options);
    }
}
