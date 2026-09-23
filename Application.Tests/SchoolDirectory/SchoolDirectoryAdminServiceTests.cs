using Application.DTOs;
using Application.Services.Implement;
using Infrastructure.Repositories.Interface;

namespace Application.Tests.SchoolDirectory;

public sealed class SchoolDirectoryAdminServiceTests
{
    private const ulong SchoolId = 7;

    [Fact]
    public async Task CreateStudent_TrimsInputAndDefaultsStatusToActive()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var result = await service.CreateStudentAsync(
            SchoolId,
            new CreateStudentRequest(
                "  HS-1  ", "  Nguyen An  ", new DateOnly(2014, 1, 1), "  M  ",
                new DateOnly(2025, 9, 1), null, 12),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var command = Assert.IsType<CreateStudentCommand>(repository.LastCommand);
        Assert.Equal(
            (SchoolId, "HS-1", "Nguyen An", "M", "ACTIVE", 12ul),
            (command.SchoolId, command.Code, command.FullName, command.Gender,
                command.Status, command.SchoolClassId));
    }

    [Fact]
    public async Task CreateStudent_RejectsBlankCodeAndUnknownStatus()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var blank = await service.CreateStudentAsync(
            SchoolId,
            new CreateStudentRequest("   ", "An", null, null, new DateOnly(2025, 9, 1), null, 12),
            CancellationToken.None);
        var badStatus = await service.CreateStudentAsync(
            SchoolId,
            new CreateStudentRequest("HS-1", "An", null, null, new DateOnly(2025, 9, 1), "GONE", 12),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, blank.Error!.Code);
        Assert.Contains("code", blank.Error.Details!.Keys);
        Assert.Equal(SchoolDirectoryErrorCodes.Validation, badStatus.Error!.Code);
        Assert.Null(repository.LastCommand);
    }

    [Fact]
    public async Task CreateStudent_RequiresAClassForTheFirstEnrollment()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var result = await service.CreateStudentAsync(
            SchoolId,
            new CreateStudentRequest("HS-1", "An", null, null, new DateOnly(2025, 9, 1), null, 0),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains("schoolClassId", result.Error.Details!.Keys);
    }

    [Fact]
    public async Task UpdateStudent_KeepsClassOptional()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        await service.UpdateStudentAsync(
            SchoolId, 9,
            new UpdateStudentRequest(
                "HS-1", "An", null, null, new DateOnly(2025, 9, 1), "TRANSFERRED", null),
            CancellationToken.None);

        var command = Assert.IsType<UpdateStudentCommand>(repository.LastCommand);
        Assert.Null(command.SchoolClassId);
        Assert.Equal("TRANSFERRED", command.Status);
    }

    [Fact]
    public async Task CreateClass_TreatsZeroHomeroomTeacherAsNotSet()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        await service.CreateClassAsync(
            SchoolId, new CreateClassRequest(3, "C1", "6A", 4, 5, null, 0),
            CancellationToken.None);

        var command = Assert.IsType<CreateClassCommand>(repository.LastCommand);
        Assert.Null(command.HomeroomTeacherId);
        Assert.Equal("ACTIVE", command.Status);
    }

    [Fact]
    public async Task CreateClass_RejectsMissingReferences()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var result = await service.CreateClassAsync(
            SchoolId, new CreateClassRequest(0, "C1", "6A", 0, 0, null, null),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Equal(
            new[] { "academicYearId", "gradeLevelId", "schoolBranchId" },
            result.Error.Details!.Keys.Order());
        Assert.Null(repository.LastCommand);
    }

    [Theory]
    [InlineData(DirectoryWriteStatus.DuplicateStudentCode, SchoolDirectoryErrorCodes.StudentCodeDuplicate)]
    [InlineData(DirectoryWriteStatus.StudentNotFound, SchoolDirectoryErrorCodes.StudentNotFound)]
    [InlineData(DirectoryWriteStatus.SchoolNotFound, SchoolDirectoryErrorCodes.SchoolNotFound)]
    [InlineData(DirectoryWriteStatus.ClassNotFound, SchoolDirectoryErrorCodes.ClassNotFound)]
    public async Task StudentWriteFailures_MapToStableErrorCodes(
        DirectoryWriteStatus status,
        string expected)
    {
        var service = CreateService(new FakeAdminRepository { Status = status });

        var result = await service.DeleteStudentAsync(SchoolId, 9, CancellationToken.None);

        Assert.Equal(expected, result.Error!.Code);
    }

    [Theory]
    [InlineData(DirectoryWriteStatus.DuplicateClassCode, SchoolDirectoryErrorCodes.ClassCodeDuplicate)]
    [InlineData(DirectoryWriteStatus.TeacherAlreadyHomeroom, SchoolDirectoryErrorCodes.TeacherAlreadyHomeroom)]
    [InlineData(DirectoryWriteStatus.ClassHasActiveStudents, SchoolDirectoryErrorCodes.ClassHasActiveStudents)]
    public async Task ClassWriteFailures_MapToStableErrorCodes(
        DirectoryWriteStatus status,
        string expected)
    {
        var service = CreateService(new FakeAdminRepository { Status = status });

        var result = await service.DeleteClassAsync(SchoolId, 9, CancellationToken.None);

        Assert.Equal(expected, result.Error!.Code);
    }

    [Theory]
    [InlineData(DirectoryWriteStatus.BranchNotInSchool, "schoolBranchId")]
    [InlineData(DirectoryWriteStatus.AcademicYearInvalid, "academicYearId")]
    [InlineData(DirectoryWriteStatus.GradeLevelNotFound, "gradeLevelId")]
    [InlineData(DirectoryWriteStatus.TeacherNotInSchool, "gradeLevelId")]
    public async Task OutOfSchoolReferences_BecomeFieldValidationErrors(
        DirectoryWriteStatus status,
        string field)
    {
        var service = CreateService(new FakeAdminRepository { Status = status });

        var result = await service.CreateClassAsync(
            SchoolId, new CreateClassRequest(3, "C1", "6A", 4, 5, null, null),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains(field, result.Error.Details!.Keys);
    }

    [Fact]
    public async Task SuccessfulWrite_ReturnsTheSameShapeAsTheGetRoute()
    {
        var service = CreateService(new FakeAdminRepository());

        var result = await service.DeleteClassAsync(SchoolId, 42, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(42ul, result.Value!.Class.Id);
    }

    [Fact]
    public async Task TransferClass_PassesTheTargetAndEffectiveDateToTheRepository()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var result = await service.TransferStudentClassAsync(
            SchoolId, 9, new TransferStudentClassRequest(21, new DateOnly(2025, 11, 1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var command = Assert.IsType<TransferStudentClassCommand>(repository.LastCommand);
        Assert.Equal(
            (SchoolId, 9ul, 21ul, (DateOnly?)new DateOnly(2025, 11, 1)),
            (command.SchoolId, command.StudentId, command.SchoolClassId, command.EffectiveOn));
    }

    [Fact]
    public async Task TransferClass_RequiresATargetClass()
    {
        var repository = new FakeAdminRepository();
        var service = CreateService(repository);

        var result = await service.TransferStudentClassAsync(
            SchoolId, 9, new TransferStudentClassRequest(0, null), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains("schoolClassId", result.Error.Details!.Keys);
        Assert.Null(repository.LastCommand);
    }

    [Theory]
    [InlineData(DirectoryWriteStatus.StudentNotEnrolledInYear, "schoolClassId")]
    [InlineData(DirectoryWriteStatus.InvalidEffectiveDate, "effectiveOn")]
    public async Task TransferClassFailures_BecomeFieldValidationErrors(
        DirectoryWriteStatus status,
        string field)
    {
        var service = CreateService(new FakeAdminRepository { Status = status });

        var result = await service.TransferStudentClassAsync(
            SchoolId, 9, new TransferStudentClassRequest(21, null), CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains(field, result.Error.Details!.Keys);
    }

    [Fact]
    public async Task UpdateStudent_PointsToTheTransferRouteWhenTheClassWouldChange()
    {
        var service = CreateService(
            new FakeAdminRepository { Status = DirectoryWriteStatus.UseClassTransfer });

        var result = await service.UpdateStudentAsync(
            SchoolId, 9,
            new UpdateStudentRequest(
                "HS-1", "An", null, null, new DateOnly(2025, 9, 1), null, 21),
            CancellationToken.None);

        Assert.Equal(SchoolDirectoryErrorCodes.Validation, result.Error!.Code);
        Assert.Contains("chuyển lớp", result.Error.Details!["schoolClassId"][0]);
    }

    private static SchoolDirectoryAdminService CreateService(FakeAdminRepository repository) =>
        new(repository, new SchoolDirectoryService(new FakeSchoolDirectoryRepository()));

    private sealed class FakeAdminRepository : ISchoolDirectoryAdminRepository
    {
        public DirectoryWriteStatus Status { get; init; } = DirectoryWriteStatus.Success;

        public object? LastCommand { get; private set; }

        public Task<DirectoryWriteResult<ulong>> CreateStudentAsync(
            CreateStudentCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Result();
        }

        public Task<DirectoryWriteResult<ulong>> UpdateStudentAsync(
            UpdateStudentCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Result();
        }

        public Task<DirectoryWriteResult<ulong>> TransferStudentClassAsync(
            TransferStudentClassCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Result(command.StudentId);
        }

        public Task<DirectoryWriteResult<ulong>> DeactivateStudentAsync(
            ulong schoolId, ulong studentId, CancellationToken cancellationToken) =>
            Result(studentId);

        public Task<DirectoryWriteResult<ulong>> CreateClassAsync(
            CreateClassCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Result();
        }

        public Task<DirectoryWriteResult<ulong>> UpdateClassAsync(
            UpdateClassCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Result(command.ClassId);
        }

        public Task<DirectoryWriteResult<ulong>> DeactivateClassAsync(
            ulong schoolId, ulong classId, CancellationToken cancellationToken) =>
            Result(classId);

        private Task<DirectoryWriteResult<ulong>> Result(ulong id = 1) =>
            Task.FromResult(Status is DirectoryWriteStatus.Success
                ? DirectoryWriteResult<ulong>.Ok(id)
                : DirectoryWriteResult<ulong>.Fail(Status));
    }
}
