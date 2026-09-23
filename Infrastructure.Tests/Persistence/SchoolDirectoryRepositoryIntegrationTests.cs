using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

// Runs against a dedicated MySQL database (ConnectionStrings__SchoolDirectoryTest) with all
// migrations applied. Fixture ids live in 9800-9899 and are removed after every test.
[Collection(SchoolDirectoryDatabaseCollection.Name)]
public sealed class SchoolDirectoryRepositoryIntegrationTests
{
    private const ulong ActorA = 9861;
    private const ulong ActorB = 9862;
    private const ulong ActorNoBranch = 9863;
    private const ulong ActiveClassA = 9841;
    private const ulong OtherActiveClassA = 9842;
    private const ulong OldClassA = 9843;
    private const ulong ClassB = 9851;
    private const ulong Student1 = 9881;
    private const ulong Student2 = 9882;
    private const ulong Student3 = 9883;
    private const ulong Student4 = 9884;
    private const ulong Student5 = 9885;

    [Fact]
    public async Task ListStudents_IsScopedToActorsSchool()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var a = await repository.ListStudentsAsync(Students(ActorA), CancellationToken.None);
            var b = await repository.ListStudentsAsync(Students(ActorB), CancellationToken.None);

            Assert.Equal(
                new[] { Student3, Student1, Student5, Student2 },
                a.Value!.Items.Select(x => x.Id));
            Assert.Equal(new[] { Student3, Student4 }, b.Value!.Items.Select(x => x.Id));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ListStudents_AppliesSearchAndAllFiltersBeforePaging()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var search = await repository.ListStudentsAsync(
                Students(ActorA) with { Search = "nguyen" }, CancellationToken.None);
            var byCode = await repository.ListStudentsAsync(
                Students(ActorA) with { Search = "HS-A2" }, CancellationToken.None);
            var status = await repository.ListStudentsAsync(
                Students(ActorA) with { Status = StudentStatusCodes.TemporaryLeave }, CancellationToken.None);
            var byClass = await repository.ListStudentsAsync(
                Students(ActorA) with { ClassId = OtherActiveClassA }, CancellationToken.None);
            var byGrade = await repository.ListStudentsAsync(
                Students(ActorA) with { GradeLevelId = 9831 }, CancellationToken.None);
            var foreignClass = await repository.ListStudentsAsync(
                Students(ActorA) with { ClassId = ClassB }, CancellationToken.None);
            var paged = await repository.ListStudentsAsync(
                Students(ActorA) with { Page = 2, PageSize = 2 }, CancellationToken.None);

            Assert.Equal(new[] { Student1 }, search.Value!.Items.Select(x => x.Id));
            Assert.Equal(new[] { Student2 }, byCode.Value!.Items.Select(x => x.Id));
            Assert.Equal(new[] { Student2 }, status.Value!.Items.Select(x => x.Id));
            Assert.Equal(new[] { Student2 }, byClass.Value!.Items.Select(x => x.Id));
            // Student5 only has a TRANSFERRED_OUT row this year, so no longer counts as "current".
            Assert.Equal(new[] { Student1, Student2 }, byGrade.Value!.Items.Select(x => x.Id).Order());
            Assert.Empty(foreignClass.Value!.Items);
            Assert.Equal(4, paged.Value!.TotalCount);
            Assert.Equal(new[] { Student5, Student2 }, paged.Value.Items.Select(x => x.Id));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task GetStudent_ReturnsOrderedAcademicHistoryAndCurrentClass()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var result = await repository.GetStudentAsync(DirectoryScope.FromActor(ActorA), Student1, CancellationToken.None);

            var detail = result.Value!;
            Assert.Equal(new[] { ActiveClassA, OldClassA }, detail.History.Select(x => x.ClassId));
            Assert.Equal("ACTIVE", detail.History[0].AcademicYearStatus);
            Assert.Equal("Cô Lan", detail.History[0].HomeroomTeacherName);
            Assert.Null(detail.History[1].HomeroomTeacherId);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ListClasses_CountsOnlyActiveEnrollments()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var result = await repository.ListClassesAsync(
                new ClassDirectoryFilter(DirectoryScope.FromActor(ActorA), null, null, null, null, null, 1, 20),
                CancellationToken.None);

            var items = result.Value!.Items;
            Assert.Equal(new[] { ActiveClassA, OtherActiveClassA }, items.Select(x => x.Id));
            Assert.Equal(new[] { 1, 1 }, items.Select(x => x.StudentCount));
            Assert.Equal("Cô Lan", items[0].HomeroomTeacherName);
            Assert.Null(items[1].HomeroomTeacherId);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ListClasses_WithExplicitYearReturnsThatYearsClasses()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var result = await repository.ListClassesAsync(
                new ClassDirectoryFilter(DirectoryScope.FromActor(ActorA), null, null, 9820, null, null, 1, 20),
                CancellationToken.None);

            Assert.Equal(new[] { OldClassA }, result.Value!.Items.Select(x => x.Id));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task GetClass_ReturnsPagedRosterWithStudentIds()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var first = await repository.GetClassAsync(
                DirectoryScope.FromActor(ActorA), ActiveClassA, 1, 1, CancellationToken.None);
            var second = await repository.GetClassAsync(
                DirectoryScope.FromActor(ActorA), ActiveClassA, 2, 1, CancellationToken.None);

            Assert.Equal(2, first.Value!.Students.TotalCount);
            Assert.Equal(new[] { Student1 }, first.Value.Students.Items.Select(x => x.StudentId));
            Assert.Equal(new[] { Student5 }, second.Value!.Students.Items.Select(x => x.StudentId));
            Assert.Equal("6A", first.Value.Students.Items[0].ClassName);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task DetailOutsideActorsSchool_ReturnsNotFound()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var student = await repository.GetStudentAsync(DirectoryScope.FromActor(ActorB), Student1, CancellationToken.None);
            var schoolClass = await repository.GetClassAsync(
                DirectoryScope.FromActor(ActorA), ClassB, 1, 20, CancellationToken.None);

            Assert.Equal(DirectoryReadStatus.NotFound, student.Status);
            Assert.Equal(DirectoryReadStatus.NotFound, schoolClass.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task TransferredStudent_DoesNotExposeDestinationSchoolEnrollment()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var fromA = await repository.GetStudentAsync(DirectoryScope.FromActor(ActorA), Student3, CancellationToken.None);
            var fromB = await repository.GetStudentAsync(DirectoryScope.FromActor(ActorB), Student3, CancellationToken.None);
            var listA = await repository.ListStudentsAsync(
                Students(ActorA) with { Search = "Le Cuong" }, CancellationToken.None);
            var listB = await repository.ListStudentsAsync(
                Students(ActorB) with { Search = "Le Cuong" }, CancellationToken.None);

            Assert.Equal(new[] { OldClassA }, fromA.Value!.History.Select(x => x.ClassId));
            Assert.Equal(new[] { ClassB }, fromB.Value!.History.Select(x => x.ClassId));
            Assert.Equal("TRANSFERRED", fromA.Value.Status);
            Assert.Null(listA.Value!.Items.Single().ClassName);
            Assert.Equal("6A", listB.Value!.Items.Single().ClassName);
            Assert.Equal(ClassB, listB.Value.Items.Single().ClassId);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ActorWithoutSchoolScope_IsRejected()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var noBranch = await repository.ListStudentsAsync(
                Students(ActorNoBranch), CancellationToken.None);
            var unknown = await repository.ListClassesAsync(
                new ClassDirectoryFilter(DirectoryScope.FromActor(9899), null, null, null, null, null, 1, 20),
                CancellationToken.None);

            Assert.Equal(DirectoryReadStatus.SchoolScopeMissing, noBranch.Status);
            Assert.Equal(DirectoryReadStatus.ActorNotFound, unknown.Status);
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    [Fact]
    public async Task ReferenceData_ReturnsOnlyActorsSchoolOptions()
    {
        await using var context = await SeedAsync();
        try
        {
            var repository = new SchoolDirectoryRepository(context);

            var result = await repository.GetReferenceDataAsync(DirectoryScope.FromActor(ActorA), null, CancellationToken.None);

            var data = result.Value!;
            Assert.Equal(new ulong[] { 9811 }, data.SchoolBranches.Select(x => x.Id));
            Assert.Equal(new[] { ActiveClassA, OtherActiveClassA }, data.Classes.Select(x => x.Id));
            Assert.Equal(new ulong[] { 9821, 9820 }, data.AcademicYears.Select(x => x.Id));
        }
        finally
        {
            await CleanupAsync(context);
        }
    }

    private static StudentDirectoryFilter Students(ulong actor) =>
        new(DirectoryScope.FromActor(actor), null, null, null, null, null, 1, 20);

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var context = CreateContext();
        await CleanupAsync(context);

        var oldYear = new AcademicYear
        {
            Id = 9820, Name = "2024-2025", Status = "CLOSED",
            StartDate = new DateOnly(2024, 9, 1), EndDate = new DateOnly(2025, 6, 30)
        };
        oldYear.AssignCode("T9820");
        var activeYear = new AcademicYear
        {
            Id = 9821, Name = "2025-2026", Status = "ACTIVE",
            StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 6, 30)
        };
        activeYear.AssignCode("T9821");
        context.AcademicYears.AddRange(oldYear, activeYear);
        context.GradeLevels.AddRange(
            new GradeLevel { Id = 9831, Name = "Khối 6" },
            new GradeLevel { Id = 9832, Name = "Khối 7" });
        context.Schools.AddRange(
            new School { Id = 9801, Code = "T9801", Name = "Trường A" },
            new School { Id = 9802, Code = "T9802", Name = "Trường B" });
        context.SchoolBranches.AddRange(
            new SchoolBranch { Id = 9811, SchoolId = 9801, Code = "T9811", Name = "Cơ sở A1" },
            new SchoolBranch { Id = 9812, SchoolId = 9802, Code = "T9812", Name = "Cơ sở B1" });
        context.Users.AddRange(
            User(ActorA, 9811), User(ActorB, 9812), User(ActorNoBranch, null), User(9864, 9811, "Cô Lan"));
        await context.SaveChangesAsync();

        context.SchoolClasses.AddRange(
            Class(ActiveClassA, 9811, "6A", 9821),
            Class(OtherActiveClassA, 9811, "6B", 9821),
            Class(OldClassA, 9811, "5A", 9820),
            Class(ClassB, 9812, "6A", 9821));
        await context.SaveChangesAsync();

        context.Teachers.Add(new Teacher { Id = 9871, UserId = 9864, ClassId = ActiveClassA });
        context.Students.AddRange(
            Student(Student1, "HS-A1", "Nguyen An", StudentStatusCodes.Active),
            Student(Student2, "HS-A2", "Tran Binh", StudentStatusCodes.TemporaryLeave),
            Student(Student3, "HS-A3", "Le Cuong", StudentStatusCodes.Transferred),
            Student(Student4, "HS-B1", "Vo Dat", StudentStatusCodes.Active),
            Student(Student5, "HS-A5", "Pham Dung", StudentStatusCodes.Transferred));
        await context.SaveChangesAsync();

        context.StudentEnrollments.AddRange(
            Enrollment(Student1, OldClassA, 9820, StudentEnrollmentStatusCodes.Completed),
            Enrollment(Student1, ActiveClassA, 9821, StudentEnrollmentStatusCodes.Active),
            Enrollment(Student2, OtherActiveClassA, 9821, StudentEnrollmentStatusCodes.Active),
            Enrollment(Student3, OldClassA, 9820, StudentEnrollmentStatusCodes.TransferredOut),
            Enrollment(Student3, ClassB, 9821, StudentEnrollmentStatusCodes.Active),
            Enrollment(Student4, ClassB, 9821, StudentEnrollmentStatusCodes.Active),
            Enrollment(Student5, ActiveClassA, 9821, StudentEnrollmentStatusCodes.TransferredOut));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    private static User User(ulong id, ulong? branchId, string name = "Người dùng") => new()
    {
        Id = id, Username = $"t{id}", Email = $"t{id}@test.local", SchoolBranchId = branchId,
        PasswordHash = "x", FullName = name
    };

    private static SchoolClass Class(ulong id, ulong branchId, string name, ulong yearId) => new()
    {
        Id = id, SchoolBranchId = branchId, Code = $"C{id}", Name = name,
        AcademicYearId = yearId, GradeLevelId = 9831
    };

    private static Student Student(ulong id, string code, string name, string status) => new()
    {
        Id = id, Code = code, FullName = name, Status = status,
        AdmissionDate = new DateOnly(2024, 9, 1)
    };

    private static StudentEnrollment Enrollment(
        ulong studentId, ulong classId, ulong yearId, string status) => new()
    {
        StudentId = studentId, SchoolClassId = classId, AcademicYearId = yearId, Status = status
    };

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        await context.StudentEnrollments.Where(x => x.StudentId >= 9800 && x.StudentId < 9900).ExecuteDeleteAsync();
        await context.Teachers.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.Students.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.SchoolClasses.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.Users.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.AcademicYears.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.GradeLevels.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.SchoolBranches.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
        await context.Schools.Where(x => x.Id >= 9800 && x.Id < 9900).ExecuteDeleteAsync();
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
