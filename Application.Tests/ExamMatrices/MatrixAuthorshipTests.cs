using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Services.Implement;
using Application.Tests.Fakes;
using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Infrastructure.Exports;
using Infrastructure.Models;

namespace Application.Tests.ExamMatrices;

/// <summary>
/// Who authored and who approved a matrix, as the API returns them (matrices no longer carry a code).
/// </summary>
public sealed class MatrixAuthorshipTests
{
    private static readonly MatrixActor Pht = new(10, MatrixActorRole.Pht, 1);

    [Fact]
    public async Task Create_RecordsTheAuthorAndReturnsNameAndRoleLabel()
    {
        var repository = new FakeMatrixRepository();
        repository.People[10] = new MatrixPersonRow(10, "Trần Thị Mai", new[] { "PHT" });
        var service = CreateService(Pht, repository);

        var response = await service.CreateAsync(
            new SaveMatrixRequest("Toán 5", 100, 2, null, 10, new[] { new MatrixDetailRequest(1, "NHAN_BIET", 3, 100m) }),
            CancellationToken.None);

        var stored = Assert.Single(repository.Items);
        Assert.Equal(10UL, stored.CreatedByUserId);
        Assert.NotEqual(default, stored.CreatedAt);

        Assert.Equal("Trần Thị Mai", response.CreatedBy?.FullName);
        Assert.Equal("Phó Hiệu trưởng", response.CreatedBy?.RoleLabel);
        Assert.Null(response.ApprovedBy);
    }

    [Fact]
    public async Task ConfirmDirect_RecordsTheApprover()
    {
        var repository = new FakeMatrixRepository();
        repository.People[10] = new MatrixPersonRow(10, "Trần Thị Mai", new[] { "PHT" });
        var service = CreateService(Pht, repository);
        await service.CreateAsync(
            new SaveMatrixRequest("Toán 5", 100, 2, null, 10, new[] { new MatrixDetailRequest(1, "NHAN_BIET", 3, 100m) }),
            CancellationToken.None);

        // The fake repository does not load navigations, so give the stored matrix the branch the service checks.
        var stored = repository.Items[0];
        stored.AcademicContext = new AcademicContext { SchoolBranchId = 1 };

        var confirmed = await service.ConfirmDirectAsync(stored.Id, CancellationToken.None);

        Assert.Equal(MatrixStatusCodes.Approved, confirmed.Status);
        Assert.Equal(10UL, repository.Items[0].ApprovedByUserId);
        Assert.Equal("Trần Thị Mai", confirmed.ApprovedBy?.FullName);
        Assert.NotNull(confirmed.ApprovedAt);
    }

    [Theory]
    [InlineData(new[] { "TEAM_LEAD", "PHT" }, "Phó Hiệu trưởng")]
    [InlineData(new[] { "PHT", "HIEU_TRUONG" }, "Hiệu trưởng")]
    [InlineData(new[] { "TO_TRUONG" }, "Tổ trưởng")]
    [InlineData(new[] { "GIAO_VIEN" }, null)]
    [InlineData(new string[0], null)]
    public async Task RoleLabel_ShowsTheMostSeniorMatrixRole(string[] roleCodes, string? expected)
    {
        var uow = new FakeUnitOfWork();
        var repository = new FakeMatrixRepository();
        repository.People[5] = new MatrixPersonRow(5, "Người dùng", roleCodes);
        uow.Matrices = repository;

        var people = await TestPeople.Resolver(uow).ResolveAsync(new ulong?[] { 5 }, CancellationToken.None);

        Assert.Equal(expected, people[5].RoleLabel);
    }

    [Fact]
    public async Task Pht_CannotSeeOrChangeATeamLeadsDraft_ButSeesItOnceSubmitted()
    {
        var repository = new FakeMatrixRepository();
        var draft = new ExamMatrix
        {
            Id = 7, Name = "Của Tổ trưởng", Status = MatrixStatusCodes.Draft, TaskId = 3,
            AcademicContext = new AcademicContext { SchoolBranchId = 1 }
        };
        repository.Items.Add(draft);
        var service = CreateService(Pht, repository);

        foreach (var call in new Func<Task>[]
        {
            () => service.GetAsync(7, CancellationToken.None),
            () => service.DeleteDraftAsync(7, CancellationToken.None),
        })
        {
            var error = await Assert.ThrowsAsync<MatrixApplicationException>(call);
            Assert.Equal("Forbidden", error.Code);
        }

        draft.Status = MatrixStatusCodes.Submitted;
        Assert.Equal(MatrixStatusCodes.Submitted, (await service.GetAsync(7, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task List_HidesTaskDraftsOnlyForPht()
    {
        var repo = new FakeMatrixRepository();
        var uow = new FakeUnitOfWork { Matrices = repo, MatrixTasks = new FakeMatrixTaskRepository(), MatrixReferences = new FakeMatrixReferenceRepository() };
        await new MatrixApplicationService(uow, new CurrentUser(Pht), new Exporter(), TestPeople.Resolver(uow))
            .ListAsync(new MatrixListQuery(), CancellationToken.None);
        Assert.True(repo.LastListQuery!.HideTaskDrafts);

        var teamLead = new MatrixActor(20, MatrixActorRole.TeamLead, 1);
        await new MatrixApplicationService(uow, new CurrentUser(teamLead), new Exporter(), TestPeople.Resolver(uow))
            .ListAsync(new MatrixListQuery(), CancellationToken.None);
        Assert.False(repo.LastListQuery!.HideTaskDrafts);
    }

    [Fact]
    public async Task Resolver_SkipsMissingAndUnknownUsers()
    {
        var uow = new FakeUnitOfWork();
        var people = await TestPeople.Resolver(uow).ResolveAsync(new ulong?[] { null, 404 }, CancellationToken.None);

        Assert.Empty(people);
    }

    private static MatrixApplicationService CreateService(MatrixActor actor, FakeMatrixRepository repository)
    {
        var uow = new FakeUnitOfWork
        {
            Matrices = repository,
            MatrixTasks = new FakeMatrixTaskRepository(),
            MatrixReferences = new FakeMatrixReferenceRepository()
        };

        return new MatrixApplicationService(uow, new CurrentUser(actor), new Exporter(), TestPeople.Resolver(uow));
    }

    private sealed class Exporter : IMatrixWorkbookExporter
    {
        public byte[] Create(MatrixWorkbookModel matrix) => new byte[] { 1 };
    }

    private sealed class CurrentUser(MatrixActor actor) : IMatrixCurrentUser
    {
        public MatrixActor Actor { get; } = actor;
    }
}
