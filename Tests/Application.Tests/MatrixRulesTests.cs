using Application.Common;
using Application.Common.Security;
using Application.DTOs;
using Application.Mappings;
using Application.Services.Implement;
using Application.Services.Interface;
using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Infrastructure.Models;
using Infrastructure.Repositories.Interface;
using Infrastructure.UnitOfWork;
using Xunit;

namespace Application.Tests;

public sealed class MatrixRulesTests
{
    private const ulong Author = 1, OtherPht = 2, TeamLead = 3;
    private static readonly MatrixScope Scope = new(100, 40, 50);

    // allowedActions drives every matrix button: role × status × direct/task matrix (rows add up to 100%).
    [Theory]
    [InlineData(MatrixStatusCodes.Draft, false, Author, "View,Update,Delete,Confirm")]
    [InlineData(MatrixStatusCodes.Draft, false, OtherPht, "View,Update,Delete")]
    [InlineData(MatrixStatusCodes.Draft, false, TeamLead, "View")]
    [InlineData(MatrixStatusCodes.Draft, true, TeamLead, "View,Update,Delete,Submit")]
    [InlineData(MatrixStatusCodes.Submitted, false, Author, "View,Update,Approve,Reject")]
    [InlineData(MatrixStatusCodes.Submitted, false, OtherPht, "View")]
    [InlineData(MatrixStatusCodes.Submitted, true, Author, "View,Update,Approve,Reject")]
    [InlineData(MatrixStatusCodes.Submitted, true, OtherPht, "View")]
    [InlineData(MatrixStatusCodes.Submitted, true, TeamLead, "View")]
    [InlineData(MatrixStatusCodes.Approved, false, OtherPht, "View,Archive,Clone,Export")]
    [InlineData(MatrixStatusCodes.Approved, true, TeamLead, "View,Export")]
    [InlineData(MatrixStatusCodes.Archived, true, Author, "View,Clone,Export")]
    [InlineData(MatrixStatusCodes.Archived, false, TeamLead, "View,Export")]
    public void AllowedActions_FollowRoleStatusAndTask(string status, bool task, ulong actorId, string expected)
    {
        var matrix = Matrix(status, task);
        Assert.Equal(expected, string.Join(",", matrix.ToResponse(Actor(actorId)).AllowedActions));
    }

    [Fact]
    public void AllowedActions_OfferNoSubmitOrConfirmBelow100Percent()
    {
        var matrix = Matrix(MatrixStatusCodes.Draft, task: false);
        matrix.Details.Single().Percentage = 60;
        Assert.Equal("View,Update,Delete", string.Join(",", matrix.ToResponse(Actor(Author)).AllowedActions));
    }

    [Theory]
    [InlineData(MatrixTaskStatusCodes.NeedsRevision, "Sai tỷ lệ chương 2")]
    [InlineData(MatrixTaskStatusCodes.InProgress, null)]
    public void RejectComment_ShowsOnlyWhileTheTaskNeedsRevision(string taskStatus, string? expected)
    {
        var matrix = Matrix(MatrixStatusCodes.Draft, task: true);
        matrix.Task!.Status = taskStatus;
        var history = new TaskHistory { TaskId = 10, Content = "Sai tỷ lệ chương 2", CreateBy = Author };
        Assert.Equal(expected, matrix.ToResponse(Actor(TeamLead), lastHistory: history).RejectComment);
    }

    [Fact]
    public void Details_CarryLessonAndChapterNamesEvenWhenArchived()
    {
        var matrix = Matrix(MatrixStatusCodes.Approved, task: false);
        matrix.Details.Single().Lesson = new Lesson
        {
            Id = 5, Number = 3, Title = "Phân số", Status = "INACTIVE", ChapterId = 7,
            Chapter = new Chapter { Id = 7, Number = 2, Title = "Số và phép tính", Status = "INACTIVE" }
        };
        var detail = Assert.Single(matrix.ToResponse(Actor(Author)).Details);
        Assert.Equal(("Phân số", 3u, 7UL, "Số và phép tính", 2u),
            (detail.LessonTitle, detail.LessonNumber, detail.ChapterId, detail.ChapterTitle, detail.ChapterNumber));
    }

    [Fact]
    public void Clone_GetsCopySuffixAndRestartsAsTheCopiersDraft()
    {
        var source = Matrix(MatrixStatusCodes.Approved, task: true);
        source.Name = "Ma trận Toán 5 - HK1";
        var clone = source.CloneAsDraft(Actor(OtherPht));
        Assert.Equal("Ma trận Toán 5 - HK1 (bản sao)", clone.Name);
        Assert.Equal((MatrixStatusCodes.Draft, (ulong?)null, OtherPht), (clone.Status, clone.TaskId, clone.CreatedByUserId));
        Assert.Equal(100m, Assert.Single(clone.Details).Percentage);
    }

    [Theory]
    [InlineData(255, 245)]
    [InlineData(246, 245)]
    [InlineData(245, 245)]
    public void Clone_TrimsALongNameSoTheSuffixFits(int length, int kept)
    {
        var source = Matrix(MatrixStatusCodes.Approved, task: false);
        source.Name = new string('a', length);
        var name = source.CloneAsDraft(Actor(Author)).Name;
        Assert.Equal(new string('a', kept) + " (bản sao)", name);
        Assert.True(name.Length <= ExamMatrix.MaxNameLength);
    }

    [Fact]
    public void Clone_DoesNotSplitAnEmojiAtTheCut()
    {
        var source = Matrix(MatrixStatusCodes.Approved, task: false);
        source.Name = new string('a', 244) + "😀" + "bbbbbbbbbb";
        Assert.Equal(new string('a', 244) + " (bản sao)", source.CloneAsDraft(Actor(Author)).Name);
    }

    [Theory]
    [InlineData("33.333")]
    [InlineData("0.001")]
    public async Task Save_RejectsPercentagesWithMoreThanTwoDecimals(string percentage)
    {
        var store = new Store(Actor(Author));
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() =>
            store.Service.CreateAsync(Request(decimal.Parse(percentage, System.Globalization.CultureInfo.InvariantCulture)), default));
        Assert.Equal("InvalidRequest", error.Code);
        Assert.Null(store.Added);
    }

    [Fact]
    public async Task Save_AcceptsTwoDecimals()
    {
        var store = new Store(Actor(Author));
        var response = await store.Service.CreateAsync(Request(33.33m), default);
        Assert.Equal(33.33m, Assert.Single(response.Details).Percentage);
        Assert.NotNull(store.Added);
    }

    [Fact]
    public async Task Submit_RejectsADirectMatrixButConfirmStillWorks()
    {
        var store = new Store(Actor(Author), Matrix(MatrixStatusCodes.Draft, task: false));
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() => store.Service.SubmitAsync(9, default));
        Assert.Equal("InvalidTransition", error.Code);
        Assert.Equal(0, store.StatusWrites);

        var confirmed = await store.Service.ConfirmDirectAsync(9, default);
        Assert.Equal(MatrixStatusCodes.Approved, confirmed.Status);
        Assert.Equal(1, store.StatusWrites);
        Assert.Equal(new ulong[] { 5 }, store.CheckedLessons);
    }

    [Fact]
    public async Task Confirm_IsForbiddenToAnotherVicePrincipal()
    {
        var store = new Store(Actor(OtherPht), Matrix(MatrixStatusCodes.Draft, task: false));
        var error = await Assert.ThrowsAsync<MatrixApplicationException>(() => store.Service.ConfirmDirectAsync(9, default));
        Assert.Equal("Forbidden", error.Code);
        Assert.Equal(0, store.StatusWrites);
    }

    // A lesson archived after the last save blocks submitting, approving and confirming (422 InvalidReference).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitOrConfirm_RecheckArchivedLessons(bool task)
    {
        var store = new Store(Actor(task ? TeamLead : Author), Matrix(MatrixStatusCodes.Draft, task)) { LessonsArchived = true };
        var error = await Assert.ThrowsAsync<MatrixDomainException>(() =>
            task ? store.Service.SubmitAsync(9, default) : store.Service.ConfirmDirectAsync(9, default));
        Assert.Equal("InvalidReference", error.Code);
        Assert.Equal(0, store.StatusWrites);
    }

    private static MatrixActor Actor(ulong id) => id == TeamLead
        ? new MatrixActor(id, MatrixActorRole.TeamLead)
        : new MatrixActor(id, MatrixActorRole.Pht, Scope.BranchId);

    // Direct matrix: authored by the PHT Author. Task matrix: assigned by Author to TeamLead, who authors it.
    private static ExamMatrix Matrix(string status, bool task)
    {
        var matrix = new ExamMatrix
        {
            Id = 9, Name = "Ma trận", Status = status, TotalScore = 10,
            BranchId = Scope.BranchId, SubjectId = Scope.SubjectId, GradeLevelId = Scope.GradeLevelId,
            CreatedByUserId = task ? TeamLead : Author,
            TaskId = task ? 10 : null,
            Task = task
                ? new WorkTask { Id = 10, CreatedByUserId = Author, AssignedToUserId = TeamLead, Status = MatrixTaskStatusCodes.InProgress }
                : null
        };
        matrix.Details.Add(new MatrixDetail
        {
            LessonId = 5, CognitiveLevel = MatrixCognitiveLevels.Knowledge,
            QuestionType = MatrixQuestionTypes.MultipleChoice, QuestionCount = 2, Percentage = 100
        });
        return matrix;
    }

    private static SaveMatrixRequest Request(decimal percentage) => new(
        "Ma trận", Scope.Key, null, 10,
        [new MatrixDetailRequest(5, MatrixCognitiveLevels.Knowledge, 2, percentage)]);

    // In-memory unit of work holding one matrix; records the writes the service makes.
    private sealed class Store(MatrixActor actor, ExamMatrix? matrix = null)
        : IUnitOfWork, IMatrixRepository, IMatrixReferenceRepository, IMatrixCurrentUser, IMatrixPeopleResolver
    {
        public ExamMatrix? Added { get; private set; }
        public int StatusWrites { get; private set; }
        public ulong[] CheckedLessons { get; private set; } = [];
        public bool LessonsArchived { get; init; }

        public MatrixApplicationService Service =>
            new(this, this, null!, this, new LockedEmailNotificationService());

        public MatrixActor Actor => actor;
        public IAcademicYearRepository AcademicYears => throw new NotSupportedException();
        public IMatrixRepository Matrices => this;
        public IMatrixTaskRepository MatrixTasks => throw new NotSupportedException();
        public IMatrixReferenceRepository MatrixReferences => this;
        public Task<int> CompleteAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public void Dispose() { }

        public Task AddAsync(ExamMatrix entity, CancellationToken cancellationToken = default) { Added = entity; return Task.CompletedTask; }
        public Task DeleteAsync(ExamMatrix entity) => throw new NotSupportedException();
        public Task<PagedResult<MatrixListRow>> ListAsync(MatrixListFilter filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExamMatrix?> GetAsync(ulong id, CancellationToken cancellationToken) => Task.FromResult(id == matrix?.Id ? matrix : null);
        public Task<bool> TryUpdateStatusAsync(ExamMatrix item, string expectedStatus, CancellationToken cancellationToken) { StatusWrites++; return Task.FromResult(true); }
        public Task<bool> LockWithStatusAsync(ulong matrixId, string expectedStatus, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyDictionary<ulong, MatrixPersonRow>> GetPeopleAsync(IReadOnlyCollection<ulong> userIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetTaskStatusAsync(ulong taskId, string status, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AddTaskHistory(TaskHistory history) { }
        public Task<TaskHistory?> GetLatestTaskHistoryAsync(ulong taskId, CancellationToken cancellationToken) => Task.FromResult<TaskHistory?>(null);

        public Task EnsureValidAsync(MatrixScope scope, IReadOnlyCollection<ulong> lessonIds, CancellationToken cancellationToken, ulong? requiredBranchId = null)
        {
            CheckedLessons = lessonIds.ToArray();
            return LessonsArchived && lessonIds.Count > 0
                ? throw new MatrixDomainException("InvalidReference", "Bài học đã được lưu trữ.")
                : Task.CompletedTask;
        }
        public Task<MatrixExportInfo> GetExportInfoAsync(MatrixScope scope, IReadOnlyCollection<ulong> lessonIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EnsureAssignmentValidAsync(MatrixActor assigner, ulong assignedToUserId, MatrixScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MatrixReferenceModel> GetReferenceDataAsync(MatrixActor viewer, ulong? academicContextId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<ulong, MatrixPerson>> ResolveAsync(IEnumerable<ulong?> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<ulong, MatrixPerson>>(new Dictionary<ulong, MatrixPerson>());
    }
}
