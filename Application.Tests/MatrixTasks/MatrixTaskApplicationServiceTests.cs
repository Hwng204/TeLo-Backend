using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Services.Implement;
using Application.Tests.Fakes;
using Domain.Entities.QuestionBank;

namespace Application.Tests.MatrixTasks;

public sealed class MatrixTaskApplicationServiceTests
{
    [Fact]
    public async Task PhtCanCreateMatrixTaskWithServerOwnedScope()
    {
        var repository = new FakeMatrixTaskRepository();
        var referenceReader = new FakeMatrixReferenceRepository();
        var service = CreateService(
            new MatrixActor(10, MatrixActorRole.Pht),
            repository,
            referenceReader);

        var result = await service.CreateAsync(
            new CreateMatrixTaskRequest(20, 100, 2, null, "  Ma trận giữa kỳ  ", "  Build matrix  "),
            CancellationToken.None);

        var stored = Assert.Single(repository.Items);
        Assert.Equal("MATRIX", stored.TaskType);
        Assert.Equal(MatrixTaskStatusCodes.Assigned, stored.Status);
        Assert.Equal((ulong)100, stored.AcademicContextId);
        Assert.Equal((ulong?)2, stored.SemesterId);
        Assert.Equal("Ma trận giữa kỳ", stored.Name);
        Assert.Equal("Build matrix", stored.Description);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal((ulong)20, result.AssignedToUserId);
        Assert.Equal(1, referenceReader.ValidationCount);
    }

    [Fact]
    public async Task TeamLeadCannotCreateMatrixTask()
    {
        var service = CreateService(
            new MatrixActor(20, MatrixActorRole.TeamLead),
            new FakeMatrixTaskRepository(),
            new FakeMatrixReferenceRepository());

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            service.CreateAsync(
                new CreateMatrixTaskRequest(21, 100, null, null, "Ma trận", null),
                CancellationToken.None));

        Assert.Equal("Forbidden", exception.Code);
    }

    [Fact]
    public async Task PhtTaskListIsScopedToTheirBranchButPrincipalIsNot()
    {
        var repository = new FakeMatrixTaskRepository();
        var reader = new FakeMatrixReferenceRepository();

        await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 5), repository, reader)
            .ListAsync(new MatrixTaskQuery(), CancellationToken.None);
        Assert.Equal((ulong)5, repository.LastQuery!.BranchId);

        await CreateService(new MatrixActor(11, MatrixActorRole.Pht, null, true), repository, reader)
            .ListAsync(new MatrixTaskQuery(), CancellationToken.None);
        Assert.Null(repository.LastQuery!.BranchId);
    }

    [Fact]
    public async Task PhtCanDeleteTaskTheTeamLeadHasNotStarted()
    {
        var repository = new FakeMatrixTaskRepository();
        repository.Tasks[7] = MatrixTask(7);

        await CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository, new FakeMatrixReferenceRepository())
            .DeleteAsync(7, CancellationToken.None);

        Assert.Empty(repository.Tasks);
    }

    [Fact]
    public async Task TaskWithASavedMatrixCannotBeDeleted()
    {
        var repository = new FakeMatrixTaskRepository();
        repository.Tasks[7] = MatrixTask(7);
        repository.LinkedMatrices[7] = 70;

        var exception = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository, new FakeMatrixReferenceRepository())
                .DeleteAsync(7, CancellationToken.None));

        Assert.Equal("TaskStarted", exception.Code);
        Assert.Equal("Nhiệm vụ đã được thực hiện, không thể xóa.", exception.Message);
        Assert.Single(repository.Tasks);
    }

    [Fact]
    public async Task OnlyPhtDeletesTasksAndOnlyInTheirBranch()
    {
        var repository = new FakeMatrixTaskRepository();
        repository.Tasks[7] = MatrixTask(7);
        var reader = new FakeMatrixReferenceRepository();

        var teamLead = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(20, MatrixActorRole.TeamLead), repository, reader).DeleteAsync(7, CancellationToken.None));
        // The fake puts every context in branch 1.
        var otherBranch = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(10, MatrixActorRole.Pht, 2), repository, reader).DeleteAsync(7, CancellationToken.None));
        var missing = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            CreateService(new MatrixActor(10, MatrixActorRole.Pht, 1), repository, reader).DeleteAsync(99, CancellationToken.None));

        Assert.Equal("Forbidden", teamLead.Code);
        Assert.Equal("Forbidden", otherBranch.Code);
        Assert.Equal("NotFound", missing.Code);
        Assert.Single(repository.Tasks);
    }

    private static WorkTask MatrixTask(ulong id) => new()
    {
        Id = id,
        CreatedByUserId = 10,
        AssignedToUserId = 20,
        Status = MatrixTaskStatusCodes.Assigned,
        TaskType = "MATRIX",
        AcademicContextId = 100,
        Name = "Ma trận",
        CreatedAt = DateTime.UtcNow
    };

    private static MatrixTaskApplicationService CreateService(
        MatrixActor actor,
        FakeMatrixTaskRepository repository,
        FakeMatrixReferenceRepository referenceReader)
    {
        var uow = new FakeUnitOfWork
        {
            MatrixTasks = repository,
            MatrixReferences = referenceReader
        };

        return new MatrixTaskApplicationService(uow, new FakeCurrentUser(actor), TestPeople.Resolver(uow));
    }

    private sealed class FakeCurrentUser(MatrixActor actor) : IMatrixCurrentUser
    {
        public MatrixActor Actor { get; } = actor;
    }
}
