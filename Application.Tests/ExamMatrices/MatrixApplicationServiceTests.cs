using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Services.Implement;
using Application.Tests.Fakes;
using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;

namespace Application.Tests.ExamMatrices;

public sealed class MatrixApplicationServiceTests
{
    [Fact]
    public async Task CreateDirectDraft_StoresServerCalculatedTotals()
    {
        var repository = new FakeMatrixRepository();
        var service = CreateService(
            new MatrixActor(10, MatrixActorRole.Pht, 1),
            repository);

        var response = await service.CreateAsync(
            new SaveMatrixRequest(
                "Direct matrix",
                100,
                2,
                null,
                10,
                new[]
                {
                    new MatrixDetailRequest(1, "NHAN_BIET", 3, 100m)
                }),
            CancellationToken.None);

        var stored = Assert.Single(repository.Items);
        Assert.Null(stored.TaskId);
        Assert.Equal(MatrixStatusCodes.Draft, stored.Status);
        Assert.Equal((uint)3, response.TotalQuestions);
        Assert.Equal(10m, response.TotalScore);
    }

    [Fact]
    public async Task CreateDelegatedDraft_UsesTaskScopeInsteadOfClientScope()
    {
        var repository = new FakeMatrixRepository();
        var taskReader = new FakeMatrixTaskRepository();
        taskReader.Tasks[7] = new WorkTask
        {
            Id = 7,
            AssignedToUserId = 20,
            TaskType = "MATRIX",
            AcademicContextId = 900,
            SemesterId = 8
        };

        var service = CreateService(
            new MatrixActor(20, MatrixActorRole.TeamLead),
            repository,
            taskReader);

        var response = await service.CreateAsync(
            new SaveMatrixRequest(
                "Delegated matrix",
                100,
                2,
                7,
                10,
                new[]
                {
                    new MatrixDetailRequest(1, "NHAN_BIET", 2, 100m)
                }),
            CancellationToken.None);

        var stored = Assert.Single(repository.Items);
        Assert.Equal((ulong)900, stored.AcademicContextId);
        Assert.Equal((ulong?)8, stored.SemesterId);
        Assert.Equal((ulong?)7, stored.TaskId);
        Assert.Equal((ulong)900, response.AcademicContextId);
    }

    [Fact]
    public async Task CreateDelegatedDraft_RejectsSecondMatrixForTask()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(new ExamMatrix
        {
            Id = 88,
            TaskId = 7,
            Status = MatrixStatusCodes.Draft,
            AcademicContextId = 900
        });

        var taskReader = new FakeMatrixTaskRepository();
        taskReader.Tasks[7] = new WorkTask
        {
            Id = 7,
            AssignedToUserId = 20,
            TaskType = "MATRIX",
            AcademicContextId = 900
        };

        var service = CreateService(
            new MatrixActor(20, MatrixActorRole.TeamLead),
            repository,
            taskReader);

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            service.CreateAsync(
                new SaveMatrixRequest(
                    "Second matrix",
                    900,
                    null,
                    7,
                    10,
                    Array.Empty<MatrixDetailRequest>()),
                CancellationToken.None));

        Assert.Equal("TaskAlreadyHasMatrix", exception.Code);
    }

    [Fact]
    public async Task ConfirmDirect_UsesTransactionAndReturnsApproved()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(new ExamMatrix
        {
            Id = 1,
            Name = "Direct matrix",
            TaskId = null,
            AcademicContextId = 100,
            AcademicContext = new AcademicContext { SchoolBranchId = 1 },
            Status = MatrixStatusCodes.Draft,
            TotalScore = 10,
            Details = new List<MatrixDetail>
            {
                new()
                {
                    Id = 5,
                    ExamMatrixId = 1,
                    LessonId = 1,
                    CognitiveLevel = "NHAN_BIET",
                    QuestionType = "MULTIPLE_CHOICE",
                    QuestionCount = 2,
                    Percentage = ExamMatrix.RequiredTotalPercentage
                }
            }
        });

        var transaction = new FakeUnitOfWork();
        var service = CreateService(
            new MatrixActor(10, MatrixActorRole.Pht, 1),
            repository,
            transaction: transaction);

        var response = await service.ConfirmDirectAsync(1, CancellationToken.None);

        Assert.Equal(MatrixStatusCodes.Approved, response.Status);
        Assert.Equal(1, transaction.ExecutionCount);
        Assert.Equal(MatrixStatusCodes.Approved, repository.Items.Single().Status);
    }

    [Fact]
    public async Task Submit_ReturnsConflictWhenExpectedStatusWasLost()
    {
        var repository = new FakeMatrixRepository
        {
            StatusUpdateResult = false
        };
        repository.Items.Add(new ExamMatrix
        {
            Id = 2,
            Name = "Delegated matrix",
            TaskId = 7,
            Status = MatrixStatusCodes.Draft,
            AcademicContextId = 100,
            Details = new List<MatrixDetail> { OneDetail() },
            Task = new WorkTask
            {
                Id = 7,
                AssignedToUserId = 20
            }
        });

        var service = CreateService(
            new MatrixActor(20, MatrixActorRole.TeamLead),
            repository);

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            service.SubmitAsync(2, CancellationToken.None));

        Assert.Equal("ConcurrencyConflict", exception.Code);
    }

    [Fact]
    public async Task TeamLeadMatrixListIsScopedToAssignedTasks()
    {
        var repository = new FakeMatrixRepository();
        var service = CreateService(
            new MatrixActor(20, MatrixActorRole.TeamLead),
            repository);

        await service.ListAsync(new MatrixListQuery(), CancellationToken.None);

        Assert.Equal((ulong)20, repository.LastListQuery!.AssignedToUserId);
    }

    [Fact]
    public async Task Submit_SetsTaskSubmittedAndApproveCompletesIt()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Draft));

        await CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository)
            .SubmitAsync(3, CancellationToken.None);
        Assert.Equal((7ul, MatrixTaskStatusCodes.Submitted), repository.TaskStatuses.Single());

        repository.TaskStatuses.Clear();
        repository.Items.Single().Status = MatrixStatusCodes.Submitted;
        await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository)
            .ApproveAsync(3, CancellationToken.None);
        Assert.Equal((7ul, MatrixTaskStatusCodes.Completed), repository.TaskStatuses.Single());
    }

    [Fact]
    public async Task Reject_SendsMatrixBackToDraftWithCommentAndReturnsTaskToAssigned()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Submitted));

        var response = await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository)
            .RejectAsync(3, new RejectMatrixRequest("Sửa lại câu 3"), CancellationToken.None);

        Assert.Equal(MatrixStatusCodes.Draft, response.Status);
        Assert.Equal("Sửa lại câu 3", response.RejectComment);
        Assert.Equal((ulong)10, response.RejectedByUserId);
        Assert.Equal((7ul, MatrixTaskStatusCodes.Assigned), repository.TaskStatuses.Single());
    }

    [Fact]
    public async Task Reject_IsOnlyForPhtAndCommentIsOptionalButLimited()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Submitted));

        var forbidden = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository)
                .RejectAsync(3, null, CancellationToken.None));
        Assert.Equal("Forbidden", forbidden.Code);

        var tooLong = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository)
                .RejectAsync(3, new RejectMatrixRequest(new string('a', 1001)), CancellationToken.None));
        Assert.Equal("InvalidRequest", tooLong.Code);

        var response = await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository)
            .RejectAsync(3, null, CancellationToken.None);
        Assert.Equal(MatrixStatusCodes.Draft, response.Status);
        Assert.Null(response.RejectComment);
    }

    [Fact]
    public async Task SubmittedMatrixOffersRejectToPhtOnlyAndNoWithdraw()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Submitted));

        var pht = await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository)
            .GetAsync(3, CancellationToken.None);
        Assert.Contains("Reject", pht.AllowedActions);
        Assert.Contains("Approve", pht.AllowedActions);
        Assert.Contains("Update", pht.AllowedActions);

        var teamLead = await CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository)
            .GetAsync(3, CancellationToken.None);
        Assert.DoesNotContain("Reject", teamLead.AllowedActions);
        Assert.DoesNotContain("Withdraw", teamLead.AllowedActions);
    }

    [Fact]
    public async Task Submit_RejectsEmptyMatrix()
    {
        var repository = new FakeMatrixRepository();
        var matrix = DelegatedMatrix(MatrixStatusCodes.Draft);
        matrix.Details.Clear();
        repository.Items.Add(matrix);

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository)
                .SubmitAsync(3, CancellationToken.None));

        Assert.Equal("EmptyMatrix", exception.Code);
    }

    [Fact]
    public async Task PhtCannotReadMatrixOfAnotherBranchButPrincipalCan()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Approved));

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(10, MatrixActorRole.Pht, 2), repository)
                .GetAsync(3, CancellationToken.None));
        Assert.Equal("Forbidden", exception.Code);

        var response = await CreateService(
                new MatrixActor(11, MatrixActorRole.Pht, null, true), repository)
            .GetAsync(3, CancellationToken.None);
        Assert.Equal((ulong)3, response.Id);
    }

    [Fact]
    public async Task PhtListAndCreateAreScopedToTheirBranch()
    {
        var repository = new FakeMatrixRepository();
        var referenceReader = new FakeMatrixReferenceRepository();
        var service = CreateService(
            new MatrixActor(10, MatrixActorRole.Pht, 5),
            repository,
            referenceReader: referenceReader);

        await service.ListAsync(new MatrixListQuery(), CancellationToken.None);
        await service.CreateAsync(
            new SaveMatrixRequest("M", 100, null, null, 10, new[]
            {
                new MatrixDetailRequest(1, "NHAN_BIET", 1, 10m)
            }),
            CancellationToken.None);

        Assert.Equal((ulong)5, repository.LastListQuery!.BranchId);
        Assert.Equal((ulong)5, referenceReader.LastRequiredBranchId);
        Assert.Equal("MULTIPLE_CHOICE", repository.Items.Single().Details.Single().QuestionType);
    }

    [Fact]
    public async Task Update_ReturnsConflictWhenStatusChangedAfterRead()
    {
        var repository = new FakeMatrixRepository { LockResult = false };
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Draft));
        var taskReader = new FakeMatrixTaskRepository();
        taskReader.Tasks[7] = new WorkTask
        {
            Id = 7,
            AssignedToUserId = 20,
            TaskType = "MATRIX",
            AcademicContextId = 900
        };

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository, taskReader)
                .UpdateAsync(
                    3,
                    new SaveMatrixRequest("M", 900, null, 7, 10, new[]
                    {
                        new MatrixDetailRequest(1, "NHAN_BIET", 1, 10m)
                    }),
                    CancellationToken.None));

        Assert.Equal("ConcurrencyConflict", exception.Code);
    }

    [Fact]
    public async Task Export_IsRejectedUntilApproved()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Draft));

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository)
                .ExportAsync(3, CancellationToken.None));

        Assert.Equal("InvalidTransition", exception.Code);
    }

    [Fact]
    public async Task TeamLeadDoesNotGetPhtOnlyActions()
    {
        var repository = new FakeMatrixRepository();
        repository.Items.Add(DelegatedMatrix(MatrixStatusCodes.Approved));

        var response = await CreateService(
                new MatrixActor(20, MatrixActorRole.TeamLead), repository)
            .GetAsync(3, CancellationToken.None);

        Assert.DoesNotContain("Archive", response.AllowedActions);
        Assert.DoesNotContain("Clone", response.AllowedActions);
        Assert.Contains("Export", response.AllowedActions);
    }

    private static MatrixDetail OneDetail() => new()
    {
        Id = 9,
        LessonId = 1,
        CognitiveLevel = "NHAN_BIET",
        QuestionType = "MULTIPLE_CHOICE",
        QuestionCount = 1,
        Percentage = ExamMatrix.RequiredTotalPercentage
    };

    private static ExamMatrix DelegatedMatrix(string status) => new()
    {
        Id = 3,
        Name = "Delegated",
        Status = status,
        TaskId = 7,
        AcademicContextId = 900,
        TotalScore = 10,
        AcademicContext = new AcademicContext { SchoolBranchId = 1 },
        Details = new List<MatrixDetail> { OneDetail() },
        Task = new WorkTask { Id = 7, AssignedToUserId = 20 }
    };

    private static MatrixApplicationService CreateService(
        MatrixActor actor,
        FakeMatrixRepository repository,
        FakeMatrixTaskRepository? taskReader = null,
        FakeUnitOfWork? transaction = null,
        FakeMatrixReferenceRepository? referenceReader = null)
    {
        var uow = transaction ?? new FakeUnitOfWork();
        uow.Matrices = repository;
        uow.MatrixTasks = taskReader ?? new FakeMatrixTaskRepository();
        uow.MatrixReferences = referenceReader ?? new FakeMatrixReferenceRepository();

        return new MatrixApplicationService(
            uow,
            new FakeCurrentUser(actor),
            new FakeExporter(),
            TestPeople.Resolver(uow));
    }

    private sealed class FakeExporter : IMatrixWorkbookExporter
    {
        public byte[] Create(MatrixWorkbookModel matrix) => new byte[] { 1 };
    }

    private sealed class FakeCurrentUser(MatrixActor actor) : IMatrixCurrentUser
    {
        public MatrixActor Actor { get; } = actor;
    }
}
