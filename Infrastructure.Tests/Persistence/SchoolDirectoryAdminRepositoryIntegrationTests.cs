using Domain.Entities.Academic;
using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

// Runs against ConnectionStrings__SchoolDirectoryTest with every migration applied.
// Fixture ids live in 96000-96999 and are deleted after each test.
[Collection(SchoolDirectoryDatabaseCollection.Name)]
public sealed class SchoolDirectoryAdminRepositoryIntegrationTests
{
    private const ulong SchoolA = 96001;
    private const ulong SchoolB = 96002;
    private const ulong BranchA = 96011;
    private const ulong BranchB = 96012;
    private const ulong ActiveYear = 96021;
    private const ulong Semester = 96031;
    private const ulong Grade = 96041;
    private const ulong OtherGrade = 96042;
    private const ulong ClassA = 96051;
    private const ulong EmptyClassA = 96052;
    private const ulong ClassB = 96053;
    private const ulong ActorA = 96061;
    private const ulong Publisher = 96063;
    private const ulong FreeTeacherUser = 96064;
    private const ulong BusyTeacherUser = 96065;
    private const ulong ForeignTeacherUser = 96066;
    private const ulong FreeTeacher = 96071;
    private const ulong BusyTeacher = 96072;
    private const ulong ForeignTeacher = 96073;
    private const ulong Student = 96081;
    private const ulong Subject = 96091;
    private const ulong UnpublishedSubject = 96092;
    private const ulong PublishedScore = 87;
    private const string Province = "96";

    [Fact]
    public async Task ListStudentScores_ReturnsOnlyPublishedResultsOfTheSelectedClass()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var result = await repository.ListStudentScoresAsync(
                DirectoryScope.FromActor(ActorA), Student, ClassA, 1, 20, CancellationToken.None);

            var item = Assert.Single(result.Value!.Items);
            Assert.Equal(PublishedScore, item.TotalScore);
            Assert.Equal("Smoke Toan", item.SubjectName);
            Assert.Equal(1, result.Value.TotalCount);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ListStudentScores_RejectsClassOutsideTheStudentHistoryOrScope()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var otherClass = await repository.ListStudentScoresAsync(
                DirectoryScope.FromActor(ActorA), Student, EmptyClassA, 1, 20, CancellationToken.None);
            var otherSchool = await repository.ListStudentScoresAsync(
                DirectoryScope.ForSchool(SchoolB), Student, ClassA, 1, 20, CancellationToken.None);

            Assert.Equal(DirectoryReadStatus.NotFound, otherClass.Status);
            Assert.Equal(DirectoryReadStatus.NotFound, otherSchool.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task AdminScope_ReadsTheNamedSchoolWithoutABranchOnTheActor()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var inScope = await repository.GetStudentAsync(
                DirectoryScope.ForSchool(SchoolA), Student, CancellationToken.None);
            var otherSchool = await repository.GetStudentAsync(
                DirectoryScope.ForSchool(SchoolB), Student, CancellationToken.None);
            var unknownSchool = await repository.GetStudentAsync(
                DirectoryScope.ForSchool(96999), Student, CancellationToken.None);

            Assert.Equal(Student, inScope.Value!.Id);
            Assert.Equal(DirectoryReadStatus.NotFound, otherSchool.Status);
            Assert.Equal(DirectoryReadStatus.SchoolNotFound, unknownSchool.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task CreateStudent_WritesProfileAndFirstEnrollmentTogether()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var created = await repository.CreateStudentAsync(
                NewStudent("SMK-96-NEW"), CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, created.Status);
            var enrollment = await context.StudentEnrollments.AsNoTracking()
                .SingleAsync(e => e.StudentId == created.Value);
            Assert.Equal(ClassA, enrollment.SchoolClassId);
            Assert.Equal(ActiveYear, enrollment.AcademicYearId);
            Assert.Equal(StudentEnrollmentStatusCodes.Active, enrollment.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task CreateStudent_RejectsDuplicateCodeAndForeignClass()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var duplicate = await repository.CreateStudentAsync(
                NewStudent("SMK-96-A1"), CancellationToken.None);
            var foreignClass = await repository.CreateStudentAsync(
                NewStudent("SMK-96-NEW2") with { SchoolClassId = ClassB }, CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.DuplicateStudentCode, duplicate.Status);
            Assert.Equal(DirectoryWriteStatus.ClassNotFound, foreignClass.Status);
            Assert.Equal(1, await context.Students.CountAsync(s => s.Id >= 96000 && s.Id < 97000));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task UpdateStudent_RefusesToMoveClassAndKeepsTheEnrollmentUntouched()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var updated = await repository.UpdateStudentAsync(
                new UpdateStudentCommand(
                    SchoolA, Student, "SMK-96-A1", "Nguyen An Updated", null, null,
                    new DateOnly(2024, 9, 1), StudentStatusCodes.Active, EmptyClassA),
                CancellationToken.None);

            // Moving a student who already has a live class is a transfer; PUT must not overwrite
            // the enrollment row and lose the old class from the history.
            Assert.Equal(DirectoryWriteStatus.UseClassTransfer, updated.Status);
            var enrollment = await context.StudentEnrollments.AsNoTracking()
                .SingleAsync(e => e.StudentId == Student);
            Assert.Equal(ClassA, enrollment.SchoolClassId);
            Assert.Equal(
                "Nguyen An",
                await context.Students.AsNoTracking()
                    .Where(s => s.Id == Student).Select(s => s.FullName).SingleAsync());
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task UpdateStudent_RejectsClassAndStudentOutsideTheSchool()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var foreignClass = await repository.UpdateStudentAsync(
                new UpdateStudentCommand(
                    SchoolA, Student, "SMK-96-A1", "Nguyen An", null, null,
                    new DateOnly(2024, 9, 1), StudentStatusCodes.Active, ClassB),
                CancellationToken.None);
            var foreignStudent = await repository.UpdateStudentAsync(
                new UpdateStudentCommand(
                    SchoolB, Student, "SMK-96-A1", "Nguyen An", null, null,
                    new DateOnly(2024, 9, 1), StudentStatusCodes.Active, null),
                CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.ClassNotFound, foreignClass.Status);
            Assert.Equal(DirectoryWriteStatus.StudentNotFound, foreignStudent.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task DeactivateStudent_KeepsHistoryAndScores()
    {
        await using var context = await SeedAsync();
        try
        {
            var admin = new SchoolDirectoryAdminRepository(context);
            var read = new SchoolDirectoryRepository(context);

            var deleted = await admin.DeactivateStudentAsync(
                SchoolA, Student, CancellationToken.None);
            context.ChangeTracker.Clear();
            var detail = await read.GetStudentAsync(
                DirectoryScope.ForSchool(SchoolA), Student, CancellationToken.None);
            var scores = await read.ListStudentScoresAsync(
                DirectoryScope.ForSchool(SchoolA), Student, ClassA, 1, 20, CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, deleted.Status);
            Assert.Equal(StudentStatusCodes.Inactive, detail.Value!.Status);
            Assert.Equal(ClassA, Assert.Single(detail.Value.History).ClassId);
            Assert.Equal(
                StudentEnrollmentStatusCodes.TransferredOut,
                detail.Value.History[0].EnrollmentStatus);
            Assert.Single(scores.Value!.Items);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task DeactivatedStudent_LeavesTheActiveRosterCount()
    {
        await using var context = await SeedAsync();
        try
        {
            var admin = new SchoolDirectoryAdminRepository(context);
            var read = new SchoolDirectoryRepository(context);

            var before = await read.GetClassAsync(
                DirectoryScope.ForSchool(SchoolA), ClassA, 1, 20, CancellationToken.None);
            await admin.DeactivateStudentAsync(SchoolA, Student, CancellationToken.None);
            context.ChangeTracker.Clear();
            var after = await read.GetClassAsync(
                DirectoryScope.ForSchool(SchoolA), ClassA, 1, 20, CancellationToken.None);

            Assert.Equal(1, before.Value!.Class.StudentCount);
            Assert.Equal(0, after.Value!.Class.StudentCount);
            // The roster itself keeps the row so the class history stays readable.
            Assert.Single(after.Value.Students.Items);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task CreateClass_AssignsHomeroomTeacherAndRejectsForeignReferences()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var created = await repository.CreateClassAsync(
                NewClass("C-NEW") with { HomeroomTeacherId = FreeTeacher }, CancellationToken.None);
            context.ChangeTracker.Clear();
            var foreignBranch = await repository.CreateClassAsync(
                NewClass("C-NEW2") with { SchoolBranchId = BranchB }, CancellationToken.None);
            context.ChangeTracker.Clear();
            var foreignTeacher = await repository.CreateClassAsync(
                NewClass("C-NEW3") with { HomeroomTeacherId = ForeignTeacher }, CancellationToken.None);
            context.ChangeTracker.Clear();
            var busyTeacher = await repository.CreateClassAsync(
                NewClass("C-NEW4") with { HomeroomTeacherId = BusyTeacher }, CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.Success, created.Status);
            Assert.Equal(DirectoryWriteStatus.BranchNotInSchool, foreignBranch.Status);
            Assert.Equal(DirectoryWriteStatus.TeacherNotInSchool, foreignTeacher.Status);
            Assert.Equal(DirectoryWriteStatus.TeacherAlreadyHomeroom, busyTeacher.Status);
            Assert.Equal(
                created.Value,
                await context.Teachers.AsNoTracking()
                    .Where(t => t.Id == FreeTeacher).Select(t => t.ClassId).SingleAsync());
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task CreateClass_RejectsDuplicateCodeWithinBranchAndYear()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var duplicate = await repository.CreateClassAsync(
                NewClass("C96051"), CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.DuplicateClassCode, duplicate.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task UpdateClass_SwapsHomeroomTeacherInOneTransaction()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var updated = await repository.UpdateClassAsync(
                new UpdateClassCommand(
                    SchoolA, ClassA, BranchA, "C96051", "6A doi ten", ActiveYear, Grade,
                    SchoolClassStatusCodes.Active, FreeTeacher),
                CancellationToken.None);
            context.ChangeTracker.Clear();

            Assert.Equal(DirectoryWriteStatus.Success, updated.Status);
            Assert.Equal(
                ClassA,
                await context.Teachers.AsNoTracking()
                    .Where(t => t.Id == FreeTeacher).Select(t => t.ClassId).SingleAsync());
            Assert.Null(
                await context.Teachers.AsNoTracking()
                    .Where(t => t.Id == BusyTeacher).Select(t => t.ClassId).SingleAsync());
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task UpdateClass_KeepsAcademicYearOnceStudentsAreEnrolled()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);
            var otherYear = new AcademicYear
            {
                Id = 96022, Name = "Admin 2026-2027", Status = "CLOSED", ProvinceCode = Province,
                StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 6, 30)
            };
            otherYear.AssignCode("T96022");
            context.AcademicYears.Add(otherYear);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var moved = await repository.UpdateClassAsync(
                new UpdateClassCommand(
                    SchoolA, ClassA, BranchA, "C96051", "6A", 96022, Grade,
                    SchoolClassStatusCodes.Active, null),
                CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.AcademicYearInvalid, moved.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task DeactivateClass_BlocksWhileActiveStudentsRemain()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryAdminRepository(context);

            var blocked = await repository.DeactivateClassAsync(
                SchoolA, ClassA, CancellationToken.None);
            context.ChangeTracker.Clear();
            var empty = await repository.DeactivateClassAsync(
                SchoolA, EmptyClassA, CancellationToken.None);
            context.ChangeTracker.Clear();
            var foreign = await repository.DeactivateClassAsync(
                SchoolA, ClassB, CancellationToken.None);

            Assert.Equal(DirectoryWriteStatus.ClassHasActiveStudents, blocked.Status);
            Assert.Equal(DirectoryWriteStatus.Success, empty.Status);
            Assert.Equal(DirectoryWriteStatus.ClassNotFound, foreign.Status);
            Assert.Equal(
                SchoolClassStatusCodes.Inactive,
                await context.SchoolClasses.AsNoTracking()
                    .Where(c => c.Id == EmptyClassA).Select(c => c.Status).SingleAsync());
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static CreateStudentCommand NewStudent(string code) => new(
        SchoolA, code, "Hoc sinh moi", new DateOnly(2014, 1, 1), "M",
        new DateOnly(2025, 9, 1), StudentStatusCodes.Active, ClassA);

    private static CreateClassCommand NewClass(string code) => new(
        SchoolA, BranchA, code, "Lop moi", ActiveYear, OtherGrade,
        SchoolClassStatusCodes.Active, null);

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var context = CreateContext();
        await CleanupAsync(context);

        context.Provinces.Add(new Province { Code = Province, Name = "Tinh Admin Test" });
        var year = new AcademicYear
        {
            Id = ActiveYear, Name = "Admin 2025-2026", Status = "ACTIVE", ProvinceCode = Province,
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 6, 30)
        };
        year.AssignCode("T96021");
        context.AcademicYears.Add(year);
        context.Semesters.Add(new Semester
        {
            Id = Semester, AcademicYearId = ActiveYear, Order = 1, Name = "HK1", Status = "ACTIVE",
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 1, 15)
        });
        context.GradeLevels.AddRange(
            new GradeLevel { Id = Grade, Name = "Admin Khoi 6" },
            new GradeLevel { Id = OtherGrade, Name = "Admin Khoi 7" });
        context.Subjects.AddRange(
            new Subject { Id = Subject, Name = "Smoke Toan" },
            new Subject { Id = UnpublishedSubject, Name = "Smoke Van" });
        context.Schools.AddRange(
            new School { Id = SchoolA, Code = "T96001", Name = "Truong Admin A", ProvinceCode = Province },
            new School { Id = SchoolB, Code = "T96002", Name = "Truong Admin B", ProvinceCode = Province });
        context.SchoolBranches.AddRange(
            new SchoolBranch { Id = BranchA, SchoolId = SchoolA, Code = "T96011", Name = "Co so A" },
            new SchoolBranch { Id = BranchB, SchoolId = SchoolB, Code = "T96012", Name = "Co so B" });
        context.Users.AddRange(
            User(ActorA, BranchA), User(Publisher, BranchA), User(FreeTeacherUser, BranchA),
            User(BusyTeacherUser, BranchA), User(ForeignTeacherUser, BranchB));
        await context.SaveChangesAsync();

        context.SchoolClasses.AddRange(
            Class(ClassA, BranchA, "C96051", "6A"),
            Class(EmptyClassA, BranchA, "C96052", "6B"),
            Class(ClassB, BranchB, "C96053", "6A"));
        await context.SaveChangesAsync();

        context.Teachers.AddRange(
            new Teacher { Id = FreeTeacher, UserId = FreeTeacherUser },
            new Teacher { Id = BusyTeacher, UserId = BusyTeacherUser, ClassId = ClassA },
            new Teacher { Id = ForeignTeacher, UserId = ForeignTeacherUser });
        context.Students.Add(new Student
        {
            Id = Student, Code = "SMK-96-A1", FullName = "Nguyen An",
            AdmissionDate = new DateOnly(2024, 9, 1), Status = StudentStatusCodes.Active
        });
        await context.SaveChangesAsync();

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = Student, SchoolClassId = ClassA, AcademicYearId = ActiveYear,
            Status = StudentEnrollmentStatusCodes.Active
        });
        await SeedExamResultsAsync(context);
        context.ChangeTracker.Clear();
        return context;
    }

    // Published and unpublished results for the same student, so the score query has to filter.
    private static async Task SeedExamResultsAsync(ApplicationDbContext context)
    {
        // exam_subject_grade_levels.primary_exam_set_id is unique, so each row needs its own set.
        context.ExamSets.AddRange(
            new ExamSet
            {
                Id = 96101, Name = "Admin exam set", Status = "APPROVED",
                CreatedByUserId = Publisher, Purpose = "OFFICIAL"
            },
            new ExamSet
            {
                Id = 96102, Name = "Admin exam set 2", Status = "APPROVED",
                CreatedByUserId = Publisher, Purpose = "OFFICIAL"
            });
        context.Exams.Add(new Exam
        {
            Id = 96111, SemesterId = Semester, Name = "Thi HK1", Status = "COMPLETED",
            SchoolBranchId = BranchA,
            StartDate = new DateOnly(2025, 12, 1), EndDate = new DateOnly(2025, 12, 5)
        });
        await context.SaveChangesAsync();

        context.ExamVariants.Add(new ExamVariant { Id = 96121, ExamSetId = 96101, VariantCode = "A" });
        context.ExamSubjects.AddRange(
            new ExamSubject
            {
                Id = 96131, ExamId = 96111, SubjectId = Subject, DurationMinutes = 45,
                Status = "COMPLETED", ResultPublishedAt = new DateTime(2025, 12, 10, 8, 0, 0),
                ResultPublishedByUserId = Publisher
            },
            new ExamSubject
            {
                Id = 96132, ExamId = 96111, SubjectId = UnpublishedSubject, DurationMinutes = 45,
                Status = "COMPLETED"
            });
        await context.SaveChangesAsync();

        context.ExamSubjectGradeLevels.AddRange(
            new ExamSubjectGradeLevel
            {
                Id = 96141, ExamSubjectId = 96131, GradeLevelId = Grade,
                PrimaryExamSetId = 96101, Status = "READY"
            },
            new ExamSubjectGradeLevel
            {
                Id = 96142, ExamSubjectId = 96132, GradeLevelId = Grade,
                PrimaryExamSetId = 96102, Status = "READY"
            });
        await context.SaveChangesAsync();

        context.ExamRegistrations.AddRange(
            new ExamRegistration
            {
                Id = 96151, ExamSubjectGradeLevelId = 96141, StudentId = Student, Status = "REGISTERED"
            },
            new ExamRegistration
            {
                Id = 96152, ExamSubjectGradeLevelId = 96142, StudentId = Student, Status = "REGISTERED"
            });
        await context.SaveChangesAsync();

        context.ExamAttempts.AddRange(
            new ExamAttempt
            {
                Id = 96161, ExamRegistrationId = 96151, ExamVariantId = 96121, Status = "SUBMITTED",
                StartedAt = new DateTime(2025, 12, 1, 7, 0, 0),
                SubmittedAt = new DateTime(2025, 12, 1, 7, 45, 0), TotalScore = PublishedScore
            },
            new ExamAttempt
            {
                Id = 96162, ExamRegistrationId = 96152, ExamVariantId = 96121, Status = "SUBMITTED",
                StartedAt = new DateTime(2025, 12, 2, 7, 0, 0),
                SubmittedAt = new DateTime(2025, 12, 2, 7, 45, 0), TotalScore = 91
            });
        await context.SaveChangesAsync();
    }

    private static User User(ulong id, ulong? branchId) => new()
    {
        Id = id, Username = $"t{id}", Email = $"t{id}@test.local", SchoolBranchId = branchId,
        PasswordHash = "x", FullName = $"Nguoi dung {id}"
    };

    private static SchoolClass Class(ulong id, ulong branchId, string code, string name) => new()
    {
        Id = id, SchoolBranchId = branchId, Code = code, Name = name,
        AcademicYearId = ActiveYear, GradeLevelId = Grade
    };

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        context.ChangeTracker.Clear();
        await context.ExamAttempts.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.ExamRegistrations.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.ExamSubjectGradeLevels.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.ExamSubjects.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.ExamVariants.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.Exams.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.ExamSets.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        // Rows the write tests create get server-generated ids outside the fixture range, so they
        // are cleaned up by the branch and code they belong to instead.
        var fixtureClasses = context.SchoolClasses
            .Where(c => c.SchoolBranchId >= 96000 && c.SchoolBranchId < 97000)
            .Select(c => c.Id);
        await context.StudentEnrollments
            .Where(x => (x.StudentId >= 96000 && x.StudentId < 97000) ||
                fixtureClasses.Contains(x.SchoolClassId))
            .ExecuteDeleteAsync();
        await context.Teachers.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.Students
            .Where(x => (x.Id >= 96000 && x.Id < 97000) || x.Code.StartsWith("SMK-96"))
            .ExecuteDeleteAsync();
        await context.SchoolClasses
            .Where(x => x.SchoolBranchId >= 96000 && x.SchoolBranchId < 97000)
            .ExecuteDeleteAsync();
        await context.Users.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.Semesters.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.AcademicYears.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.Subjects.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.GradeLevels.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.SchoolBranches.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
        await context.Schools.Where(x => x.Id >= 96000 && x.Id < 97000).ExecuteDeleteAsync();
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
