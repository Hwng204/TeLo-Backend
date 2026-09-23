using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Repositories.Interface;

namespace Application.Tests.SchoolDirectory;

public sealed class SchoolDirectoryServiceTests
{
    private static readonly DirectoryScope Actor = DirectoryScope.FromActor(5);

    [Fact]
    public async Task ListStudents_TrimsSearchAndPreservesAllFilters()
    {
        var repository = new FakeSchoolDirectoryRepository();
        var service = new SchoolDirectoryService(repository);

        var result = await service.ListStudentsAsync(
            Actor, new StudentListQuery("  An  ", 1, 2, 3, "temporary_leave", 2, 50),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var filter = Assert.IsType<StudentDirectoryFilter>(repository.LastFilter);
        Assert.Equal((Actor, "An", 1ul, 2ul, 3ul, "TEMPORARY_LEAVE", 2, 50),
            (filter.Scope, filter.Search, filter.GradeLevelId, filter.ClassId,
                filter.SchoolBranchId, filter.Status, filter.Page, filter.PageSize));
        Assert.Equal(2, result.Value!.Page);
    }

    [Fact]
    public async Task ListStudents_PassesAdminSchoolScopeThrough()
    {
        var repository = new FakeSchoolDirectoryRepository();
        var service = new SchoolDirectoryService(repository);

        await service.ListStudentsAsync(
            DirectoryScope.ForSchool(42), new StudentListQuery(null, null, null, null, null),
            CancellationToken.None);

        var filter = Assert.IsType<StudentDirectoryFilter>(repository.LastFilter);
        Assert.Equal(42ul, filter.Scope.SchoolId);
        Assert.Null(filter.Scope.ActorUserId);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListStudents_RejectsInvalidPaging(int page, int pageSize)
    {
        var repository = new FakeSchoolDirectoryRepository();
        var service = new SchoolDirectoryService(repository);

        var result = await service.ListStudentsAsync(
            Actor, new StudentListQuery(null, null, null, null, null, page, pageSize),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Null(repository.LastFilter);
    }

    [Fact]
    public async Task ListClasses_RejectsUnknownStatusBeforeRepositoryCall()
    {
        var repository = new FakeSchoolDirectoryRepository();
        var service = new SchoolDirectoryService(repository);

        var result = await service.ListClassesAsync(
            Actor, new ClassListQuery(null, null, null, null, "BOGUS"), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains("status", result.Error.Details!.Keys);
        Assert.Null(repository.LastFilter);
    }

    [Fact]
    public async Task ListStudents_RejectsOverlongSearch()
    {
        var service = new SchoolDirectoryService(new FakeSchoolDirectoryRepository());

        var result = await service.ListStudentsAsync(
            Actor, new StudentListQuery(new string('a', 101), null, null, null, null),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task GetStudent_MapsOutOfScopeToStudentNotFound()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { Status = DirectoryReadStatus.NotFound });

        var result = await service.GetStudentAsync(Actor, 9, CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.StudentNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task GetClass_MapsOutOfScopeToClassNotFound()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { Status = DirectoryReadStatus.NotFound });

        var result = await service.GetClassAsync(Actor, 9, new PageQuery(), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ClassNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task MissingSchoolScope_MapsToForbiddenCode()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { Status = DirectoryReadStatus.SchoolScopeMissing });

        var result = await service.ListClassesAsync(
            Actor, new ClassListQuery(null, null, null, null, null), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.SchoolScopeRequired, result.Error!.Code);
    }

    [Fact]
    public async Task UnknownAdminSchool_MapsToSchoolNotFound()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { Status = DirectoryReadStatus.SchoolNotFound });

        var result = await service.ListClassesAsync(
            DirectoryScope.ForSchool(404), new ClassListQuery(null, null, null, null, null),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.SchoolNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task MissingActiveYear_MapsToUnprocessableCode()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository
            {
                Status = DirectoryReadStatus.ActiveAcademicYearNotFound
            });

        var result = await service.ListClassesAsync(
            Actor, new ClassListQuery(null, null, null, null, null), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task GetStudent_PrefersActiveYearEnrollmentAsCurrentClass()
    {
        var history = new[]
        {
            Enrollment(2, "2025-2026", "COMPLETED", 20),
            Enrollment(1, "2024-2025", "ACTIVE", 10),
        };
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { History = history });

        var result = await service.GetStudentAsync(Actor, 9, CancellationToken.None);

        Assert.Equal(10ul, result.Value!.CurrentClass!.ClassId);
        Assert.Equal(new[] { 20ul, 10ul }, result.Value.AcademicHistory.Select(h => h.ClassId));
    }

    [Fact]
    public async Task GetStudent_FallsBackToLatestClassWhenNoActiveYear()
    {
        var history = new[]
        {
            Enrollment(2, "2025-2026", "CLOSED", 20),
            Enrollment(1, "2024-2025", "CLOSED", 10),
        };
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { History = history });

        var result = await service.GetStudentAsync(Actor, 9, CancellationToken.None);

        Assert.Equal(20ul, result.Value!.CurrentClass!.ClassId);
        Assert.Null(result.Value.CurrentClass.HomeroomTeacherId);
    }

    [Fact]
    public async Task GetStudentScores_PassesClassAndPagingToRepository()
    {
        var repository = new FakeSchoolDirectoryRepository
        {
            Scores =
            [
                new StudentScoreRow(
                    1, 2, "Thi HK1", 3, "HK1", 4, "Toán",
                    new DateOnly(2025, 12, 1), 8.5m, new DateTime(2025, 12, 5))
            ]
        };
        var service = new SchoolDirectoryService(repository);

        var result = await service.GetStudentScoresAsync(
            Actor, 9, 77, new PageQuery(2, 10), CancellationToken.None);

        Assert.Equal((9ul, 77ul, 2, 10), repository.LastScoreQuery);
        Assert.Equal(8.5m, Assert.Single(result.Value!.Items).TotalScore);
        Assert.Equal(2, result.Value.Page);
    }

    [Fact]
    public async Task GetStudentScores_RejectsMissingClassAndBadPaging()
    {
        var repository = new FakeSchoolDirectoryRepository();
        var service = new SchoolDirectoryService(repository);

        var noClass = await service.GetStudentScoresAsync(
            Actor, 9, 0, new PageQuery(), CancellationToken.None);
        var badPaging = await service.GetStudentScoresAsync(
            Actor, 9, 5, new PageQuery(1, 0), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, noClass.Error!.Code);
        Assert.Equal(SchoolDirectoryErrorCodes.Validation, badPaging.Error!.Code);
        Assert.Null(repository.LastScoreQuery);
    }

    [Fact]
    public async Task GetStudentScores_MapsUnknownClassToStudentNotFound()
    {
        var service = new SchoolDirectoryService(
            new FakeSchoolDirectoryRepository { Status = DirectoryReadStatus.NotFound });

        var result = await service.GetStudentScoresAsync(
            Actor, 9, 5, new PageQuery(), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.StudentNotFound, result.Error!.Code);
    }

    private static StudentEnrollmentRow Enrollment(
        ulong yearId, string yearName, string yearStatus, ulong classId) =>
        new(yearId, yearName, yearStatus, 1, "Khối 6", classId, $"6A{classId}", 1, "Cơ sở 1",
            null, null, "ACTIVE");
}
