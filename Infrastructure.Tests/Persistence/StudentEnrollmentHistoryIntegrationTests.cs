using Domain.Entities.Academic;
using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Tests.Persistence;

// Class transfers and student-code reuse. Runs against ConnectionStrings__SchoolDirectoryTest with
// every migration applied. Fixture ids live in 95000-95999 (province "95") and are deleted after
// each test.
[Collection(SchoolDirectoryDatabaseCollection.Name)]
public sealed class StudentEnrollmentHistoryIntegrationTests
{
    private const ulong School = 95001;
    private const ulong OtherSchool = 95002;
    private const ulong Branch = 95011;
    private const ulong OtherBranch = 95012;
    private const ulong Year = 95021;
    private const ulong OldYear = 95022;
    private const ulong Semester = 95031;
    private const ulong Grade = 95041;
    private const ulong Class6A = 95051;
    private const ulong Class6B = 95052;
    private const ulong InactiveClass = 95053;
    private const ulong ForeignClass = 95054;
    private const ulong OldYearClass = 95055;
    private const ulong Publisher = 95061;
    private const ulong Student = 95081;
    private const ulong Subject = 95091;
    private const string Province = "95";
    private const string StudentCode = "HIS-95-1";

    [Fact]
    public async Task Transfer_KeepsBothPeriodsAndLeavesExactlyOneActive()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var result = await repository.TransferStudentClassAsync(
                Transfer(Class6B, new DateOnly(2025, 11, 1)), CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, result.Status);
            var rows = await context.StudentEnrollments.AsNoTracking()
                .Where(e => e.StudentId == Student).OrderBy(e => e.Id).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(Class6A, rows[0].SchoolClassId);
            Assert.Equal(StudentEnrollmentStatusCodes.TransferredOut, rows[0].Status);
            Assert.Equal(new DateOnly(2025, 11, 1), rows[0].EndedOn);
            Assert.Equal(Class6B, rows[1].SchoolClassId);
            Assert.Equal(StudentEnrollmentStatusCodes.Active, rows[1].Status);
            Assert.Equal(new DateOnly(2025, 11, 1), rows[1].StartedOn);
            Assert.Equal(1, rows.Count(e => e.Status == StudentEnrollmentStatusCodes.Active));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transfer_HistoryListsBothClassesAndCurrentClassIsTheNewOne()
    {
        await using var context = await SeedAsync();
        try
        {
            var admin = new SchoolDirectoryAdminRepository(context);
            var read = new SchoolDirectoryRepository(context);
            await admin.TransferStudentClassAsync(
                Transfer(Class6B, new DateOnly(2025, 11, 1)), CancellationToken.None);
            context.ChangeTracker.Clear();

            var detail = await read.GetStudentAsync(
                DirectoryScope.ForSchool(School), Student, CancellationToken.None);

            var history = detail.Value!.History;
            Assert.Equal(new[] { Class6B, Class6A }, history.Select(h => h.ClassId));
            Assert.Equal(
                new[] { StudentEnrollmentStatusCodes.Active, StudentEnrollmentStatusCodes.TransferredOut },
                history.Select(h => h.EnrollmentStatus));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transfer_ScoresFollowTheStudentIntoBothClasses()
    {
        await using var context = await SeedAsync();
        try
        {
            var admin = new SchoolDirectoryAdminRepository(context);
            var read = new SchoolDirectoryRepository(context);
            await admin.TransferStudentClassAsync(
                Transfer(Class6B, new DateOnly(2025, 11, 1)), CancellationToken.None);
            context.ChangeTracker.Clear();

            var fromOld = await read.ListStudentScoresAsync(
                DirectoryScope.ForSchool(School), Student, Class6A, 1, 20, CancellationToken.None);
            var fromNew = await read.ListStudentScoresAsync(
                DirectoryScope.ForSchool(School), Student, Class6B, 1, 20, CancellationToken.None);

            // Scores belong to the student, not to a class: the same published result shows up
            // whichever class of that year is chosen.
            Assert.Equal(
                fromOld.Value!.Items.Select(x => x.AttemptId),
                fromNew.Value!.Items.Select(x => x.AttemptId));
            Assert.Single(fromNew.Value.Items);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transfer_DoesNotCountTheStudentTwiceInRostersFiltersOrLists()
    {
        await using var context = await SeedAsync();
        try
        {
            var admin = new SchoolDirectoryAdminRepository(context);
            var read = new SchoolDirectoryRepository(context);
            var scope = DirectoryScope.ForSchool(School);
            await admin.TransferStudentClassAsync(
                Transfer(Class6B, new DateOnly(2025, 11, 1)), CancellationToken.None);
            context.ChangeTracker.Clear();

            var list = await read.ListStudentsAsync(
                new StudentDirectoryFilter(scope, null, null, null, null, null, 1, 20),
                CancellationToken.None);
            var inOldClass = await read.ListStudentsAsync(
                new StudentDirectoryFilter(scope, null, null, Class6A, null, null, 1, 20),
                CancellationToken.None);
            var inNewClass = await read.ListStudentsAsync(
                new StudentDirectoryFilter(scope, null, null, Class6B, null, null, 1, 20),
                CancellationToken.None);
            var classes = await read.ListClassesAsync(
                new ClassDirectoryFilter(scope, null, null, Year, null, SchoolClassStatusCodes.Active, 1, 20),
                CancellationToken.None);

            var row = Assert.Single(list.Value!.Items);
            Assert.Equal(Class6B, row.ClassId);
            Assert.Empty(inOldClass.Value!.Items);
            Assert.Single(inNewClass.Value!.Items);
            Assert.Equal(
                new[] { 0, 1 },
                classes.Value!.Items.OrderBy(c => c.Name).Select(c => c.StudentCount));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transfer_IsIdempotentWhenTheStudentIsAlreadyInThatClass()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var result = await repository.TransferStudentClassAsync(
                Transfer(Class6A, null), CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, result.Status);
            Assert.Equal(1, await context.StudentEnrollments
                .CountAsync(e => e.StudentId == Student));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Transfer_RejectsBadTargetsAndDatesWithoutChangingAnything()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var unknown = await repository.TransferStudentClassAsync(
                Transfer(95999, null), CancellationToken.None);
            var otherSchool = await repository.TransferStudentClassAsync(
                Transfer(ForeignClass, null), CancellationToken.None);
            var inactive = await repository.TransferStudentClassAsync(
                Transfer(InactiveClass, null), CancellationToken.None);
            var noEnrollmentThatYear = await repository.TransferStudentClassAsync(
                Transfer(OldYearClass, null), CancellationToken.None);
            var beforeStart = await repository.TransferStudentClassAsync(
                Transfer(Class6B, new DateOnly(2020, 1, 1)), CancellationToken.None);
            var wrongSchool = await repository.TransferStudentClassAsync(
                new TransferStudentClassCommand(OtherSchool, Student, Class6B, null),
                CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.ClassNotFound, unknown.Status);
            Assert.Equal(DirectoryWriteStatus.ClassNotFound, otherSchool.Status);
            Assert.Equal(DirectoryWriteStatus.ClassNotFound, inactive.Status);
            Assert.Equal(DirectoryWriteStatus.StudentNotEnrolledInYear, noEnrollmentThatYear.Status);
            Assert.Equal(DirectoryWriteStatus.InvalidEffectiveDate, beforeStart.Status);
            Assert.Equal(DirectoryWriteStatus.StudentNotFound, wrongSchool.Status);
            Assert.Equal(1, await context.StudentEnrollments
                .CountAsync(e => e.StudentId == Student));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task Database_RefusesASecondActiveEnrollmentInTheSameYear()
    {
        await using var context = await SeedAsync();
        try
        {
            context.StudentEnrollments.Add(new StudentEnrollment
            {
                StudentId = Student, SchoolClassId = Class6B, AcademicYearId = Year,
                Status = StudentEnrollmentStatusCodes.Active,
                StartedOn = new DateOnly(2025, 10, 1)
            });

            var failure = await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());

            Assert.Equal(1062, Assert.IsType<MySqlException>(failure.InnerException).Number);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task UpdateStudent_PlacesAStudentWhoHasNoLiveClassThatYear()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);
            await repository.DeactivateStudentAsync(School, Student, CancellationToken.None);
            context.ChangeTracker.Clear();

            var placed = await repository.UpdateStudentAsync(
                new UpdateStudentCommand(
                    School, Student, StudentCode, "Nguyen An", null, null,
                    new DateOnly(2024, 9, 1), StudentStatusCodes.Active, Class6B),
                CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, placed.Status);
            var rows = await context.StudentEnrollments.AsNoTracking()
                .Where(e => e.StudentId == Student).OrderBy(e => e.Id).ToListAsync();
            Assert.Equal(
                new[] { StudentEnrollmentStatusCodes.TransferredOut, StudentEnrollmentStatusCodes.Active },
                rows.Select(r => r.Status));
            Assert.Equal(Class6B, rows[1].SchoolClassId);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task DeletedStudentsCode_CanBeReusedButALiveOneCannot()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var whileLive = await repository.CreateStudentAsync(
                NewStudent(StudentCode), CancellationToken.None);
            await repository.DeactivateStudentAsync(School, Student, CancellationToken.None);
            context.ChangeTracker.Clear();
            var afterDelete = await repository.CreateStudentAsync(
                NewStudent(StudentCode), CancellationToken.None);
            context.ChangeTracker.Clear();
            var again = await repository.CreateStudentAsync(
                NewStudent(StudentCode), CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.DuplicateStudentCode, whileLive.Status);
            Assert.Equal(DirectoryWriteStatus.Success, afterDelete.Status);
            Assert.Equal(DirectoryWriteStatus.DuplicateStudentCode, again.Status);
            Assert.Equal(2, await context.Students.CountAsync(s => s.Code == StudentCode));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ReactivatingAStudentWhoseCodeWasTakenIsReportedAsADuplicate()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);
            await repository.DeactivateStudentAsync(School, Student, CancellationToken.None);
            context.ChangeTracker.Clear();
            await repository.CreateStudentAsync(NewStudent(StudentCode), CancellationToken.None);
            context.ChangeTracker.Clear();

            var reactivated = await repository.UpdateStudentAsync(
                new UpdateStudentCommand(
                    School, Student, StudentCode, "Nguyen An", null, null,
                    new DateOnly(2024, 9, 1), StudentStatusCodes.Active, null),
                CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.DuplicateStudentCode, reactivated.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static TransferStudentClassCommand Transfer(ulong classId, DateOnly? on) =>
        new(School, Student, classId, on);

    private static CreateStudentCommand NewStudent(string code) => new(
        School, code, "Hoc sinh moi", new DateOnly(2014, 1, 1), "NAM",
        new DateOnly(2025, 9, 1), StudentStatusCodes.Active, Class6A);

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var context = CreateContext();
        await CleanupAsync(context);

        context.Provinces.Add(new Province { Code = Province, Name = "Tinh History Test" });
        var year = new AcademicYear
        {
            Id = Year, Name = "History 2025-2026", Status = "ACTIVE", ProvinceCode = Province,
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 6, 30)
        };
        year.AssignCode("T95021");
        var oldYear = new AcademicYear
        {
            Id = OldYear, Name = "History 2024-2025", Status = "CLOSED", ProvinceCode = Province,
            StartDate = new DateOnly(2024, 9, 1), EndDate = new DateOnly(2025, 6, 30)
        };
        oldYear.AssignCode("T95022");
        context.AcademicYears.AddRange(year, oldYear);
        context.Semesters.Add(new Semester
        {
            Id = Semester, AcademicYearId = Year, Order = 1, Name = "HK1", Status = "ACTIVE",
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 1, 15)
        });
        context.GradeLevels.Add(new GradeLevel { Id = Grade, Name = "History Khoi 6" });
        context.Subjects.Add(new Subject { Id = Subject, Name = "History Toan" });
        context.Schools.AddRange(
            new School { Id = School, Code = "T95001", Name = "Truong History A", ProvinceCode = Province },
            new School { Id = OtherSchool, Code = "T95002", Name = "Truong History B", ProvinceCode = Province });
        context.SchoolBranches.AddRange(
            new SchoolBranch { Id = Branch, SchoolId = School, Code = "T95011", Name = "Co so A" },
            new SchoolBranch { Id = OtherBranch, SchoolId = OtherSchool, Code = "T95012", Name = "Co so B" });
        context.Users.Add(new User
        {
            Id = Publisher, Username = "t95061", Email = "t95061@test.local",
            SchoolBranchId = Branch, PasswordHash = "x", FullName = "Nguoi cong bo"
        });
        await context.SaveChangesAsync();

        context.SchoolClasses.AddRange(
            Class(Class6A, Branch, "H6A", "6A", Year),
            Class(Class6B, Branch, "H6B", "6B", Year),
            Class(InactiveClass, Branch, "H6X", "6X", Year, SchoolClassStatusCodes.Inactive),
            Class(ForeignClass, OtherBranch, "H6F", "6F", Year),
            Class(OldYearClass, Branch, "H5A", "5A", OldYear));
        context.Students.Add(new Student
        {
            Id = Student, Code = StudentCode, FullName = "Nguyen An",
            AdmissionDate = new DateOnly(2024, 9, 1), Status = StudentStatusCodes.Active
        });
        await context.SaveChangesAsync();

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = Student, SchoolClassId = Class6A, AcademicYearId = Year,
            Status = StudentEnrollmentStatusCodes.Active, StartedOn = new DateOnly(2025, 9, 1)
        });
        await SeedPublishedScoreAsync(context);
        context.ChangeTracker.Clear();
        return context;
    }

    private static async Task SeedPublishedScoreAsync(ApplicationDbContext context)
    {
        context.ExamSets.Add(new ExamSet
        {
            Id = 95101, Name = "History set", Status = "APPROVED",
            CreatedByUserId = Publisher, Purpose = "OFFICIAL"
        });
        context.Exams.Add(new Exam
        {
            Id = 95111, SemesterId = Semester, Name = "Thi HK1", Status = "COMPLETED",
            SchoolBranchId = Branch,
            StartDate = new DateOnly(2025, 12, 1), EndDate = new DateOnly(2025, 12, 5)
        });
        await context.SaveChangesAsync();

        context.ExamVariants.Add(new ExamVariant { Id = 95121, ExamSetId = 95101, VariantCode = "A" });
        context.ExamSubjects.Add(new ExamSubject
        {
            Id = 95131, ExamId = 95111, SubjectId = Subject, DurationMinutes = 45,
            Status = "COMPLETED", ResultPublishedAt = new DateTime(2025, 12, 10, 8, 0, 0),
            ResultPublishedByUserId = Publisher
        });
        await context.SaveChangesAsync();

        context.ExamSubjectGradeLevels.Add(new ExamSubjectGradeLevel
        {
            Id = 95141, ExamSubjectId = 95131, GradeLevelId = Grade,
            PrimaryExamSetId = 95101, Status = "READY"
        });
        await context.SaveChangesAsync();

        context.ExamRegistrations.Add(new ExamRegistration
        {
            Id = 95151, ExamSubjectGradeLevelId = 95141, StudentId = Student, Status = "REGISTERED"
        });
        await context.SaveChangesAsync();

        context.ExamAttempts.Add(new ExamAttempt
        {
            Id = 95161, ExamRegistrationId = 95151, ExamVariantId = 95121, Status = "SUBMITTED",
            StartedAt = new DateTime(2025, 12, 1, 7, 0, 0),
            SubmittedAt = new DateTime(2025, 12, 1, 7, 45, 0), TotalScore = 8.5m
        });
        await context.SaveChangesAsync();
    }

    private static SchoolClass Class(
        ulong id, ulong branchId, string code, string name, ulong yearId,
        string status = SchoolClassStatusCodes.Active) => new()
    {
        Id = id, SchoolBranchId = branchId, Code = code, Name = name, Status = status,
        AcademicYearId = yearId, GradeLevelId = Grade
    };

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        context.ChangeTracker.Clear();
        await context.ExamAttempts.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.ExamRegistrations.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.ExamSubjectGradeLevels.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.ExamSubjects.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.ExamVariants.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.Exams.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.ExamSets.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();

        // Students created by the tests get server-generated ids outside the fixture range, so
        // they are found by code and by the branches their classes belong to.
        var fixtureClasses = context.SchoolClasses
            .Where(c => c.SchoolBranchId >= 95000 && c.SchoolBranchId < 96000)
            .Select(c => c.Id);
        await context.StudentEnrollments
            .Where(x => (x.StudentId >= 95000 && x.StudentId < 96000) ||
                fixtureClasses.Contains(x.SchoolClassId))
            .ExecuteDeleteAsync();
        await context.Students
            .Where(x => (x.Id >= 95000 && x.Id < 96000) || x.Code.StartsWith("HIS-95"))
            .ExecuteDeleteAsync();
        await context.SchoolClasses
            .Where(x => x.SchoolBranchId >= 95000 && x.SchoolBranchId < 96000)
            .ExecuteDeleteAsync();
        await context.Users.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.Semesters.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.AcademicYears.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.Subjects.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.GradeLevels.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.SchoolBranches.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.Schools.Where(x => x.Id >= 95000 && x.Id < 96000).ExecuteDeleteAsync();
        await context.Provinces.Where(x => x.Code == Province).ExecuteDeleteAsync();
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
