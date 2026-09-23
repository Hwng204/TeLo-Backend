using System.Linq;
using Domain.Entities.QuestionBank;

namespace Domain.Tests.ExamMatrices;

public sealed class ExamMatrixTests
{
    private static readonly MatrixActor Pht = new(10, MatrixActorRole.Pht);
    private static readonly MatrixActor TeamLead = new(20, MatrixActorRole.TeamLead);

    [Fact]
    public void ConfirmDirect_TransitionsDraftToApproved()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail() }, Pht);

        matrix.ConfirmDirect(Pht);

        Assert.Equal(MatrixStatusCodes.Approved, matrix.Status);
    }

    [Fact]
    public void Submit_RejectsEmptyMatrix()
    {
        var matrix = DirectDraft();

        var exception = Assert.Throws<MatrixDomainException>(() => matrix.Submit(Pht));

        Assert.Equal("EmptyMatrix", exception.Code);
    }

    [Fact]
    public void ReplaceDetails_RejectsUnknownCognitiveLevelAndDefaultsQuestionType()
    {
        var matrix = DirectDraft();

        Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail(cognitiveLevel: "HIGHER_APPLICATION") }, Pht));

        matrix.ReplaceDetails(new[] { Detail(questionType: "") }, Pht);
        Assert.Equal("MULTIPLE_CHOICE", matrix.Details.Single().QuestionType);
    }

    [Fact]
    public void ConfirmDirect_RejectsDelegatedMatrix()
    {
        var matrix = DelegatedDraft();

        var exception = Assert.Throws<MatrixDomainException>(() =>
            matrix.ConfirmDirect(Pht));

        Assert.Equal("DirectMatrixRequired", exception.Code);
    }

    [Fact]
    public void TeamLeadCanEditOnlyTheirAssignedDraft()
    {
        var matrix = DelegatedDraft(assignedToUserId: TeamLead.UserId);
        var otherTeamLead = new MatrixActor(21, MatrixActorRole.TeamLead);

        Assert.True(matrix.CanEdit(TeamLead));
        Assert.False(matrix.CanEdit(otherTeamLead));
    }

    [Fact]
    public void TeamLeadCannotEditOrRejectSubmittedMatrix()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;

        Assert.False(matrix.CanEdit(TeamLead));

        var exception = Assert.Throws<MatrixDomainException>(() =>
            matrix.Reject(TeamLead, "x", DateTime.UtcNow));
        Assert.Equal("Forbidden", exception.Code);
        Assert.Equal(MatrixStatusCodes.Submitted, matrix.Status);
    }

    [Fact]
    public void PhtRejectSendsSubmittedMatrixBackToDraftWithComment()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;
        var at = new DateTime(2026, 9, 19, 8, 0, 0, DateTimeKind.Utc);

        matrix.Reject(Pht, "  Sửa lại câu 3  ", at);

        Assert.Equal(MatrixStatusCodes.Draft, matrix.Status);
        Assert.Equal("Sửa lại câu 3", matrix.RejectComment);
        Assert.Equal(Pht.UserId, matrix.RejectedByUserId);
        Assert.Equal(at, matrix.RejectedAt);
        Assert.True(matrix.CanEdit(TeamLead));
    }

    [Fact]
    public void RejectCommentIsOptionalAndOnlyASubmittedMatrixCanBeRejected()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;
        matrix.Reject(Pht, "   ", DateTime.UtcNow);
        Assert.Null(matrix.RejectComment);

        var draft = DelegatedDraft();
        var exception = Assert.Throws<MatrixDomainException>(() =>
            draft.Reject(Pht, "x", DateTime.UtcNow));
        Assert.Equal("InvalidTransition", exception.Code);
    }

    [Fact]
    public void SubmitClearsThePreviousRejection()
    {
        var matrix = DelegatedDraft();
        matrix.ReplaceDetails(new[] { Detail() }, TeamLead);
        matrix.Status = MatrixStatusCodes.Submitted;
        matrix.Reject(Pht, "Sửa lại", DateTime.UtcNow);

        matrix.Submit(TeamLead);

        Assert.Equal(MatrixStatusCodes.Submitted, matrix.Status);
        Assert.Null(matrix.RejectComment);
        Assert.Null(matrix.RejectedByUserId);
        Assert.Null(matrix.RejectedAt);
    }

    [Fact]
    public void PhtCanEditAndApproveSubmittedMatrix()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;
        matrix.ReplaceDetails(new[] { Detail(questionCount: 2) }, Pht);

        matrix.Approve(Pht);

        Assert.Equal(MatrixStatusCodes.Approved, matrix.Status);
        Assert.Equal((uint)2, matrix.TotalQuestions);
    }

    [Fact]
    public void ApprovedMatrixCannotBeEdited()
    {
        var matrix = DirectDraft();
        matrix.Status = MatrixStatusCodes.Approved;

        var exception = Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail() }, Pht));

        Assert.Equal("MatrixNotEditable", exception.Code);
    }

    [Fact]
    public void DetailsRejectInvalidValuesAndDuplicateCells()
    {
        var matrix = DirectDraft();

        Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail(questionCount: 0) }, Pht));

        Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail(percentage: 0) }, Pht));

        Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail(percentage: 100.01m) }, Pht));

        var duplicate = new[]
        {
            Detail(cognitiveLevel: " NHAN_BIET "),
            Detail(cognitiveLevel: "nhan_biet")
        };

        var exception = Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(duplicate, Pht));

        Assert.Equal("DuplicateDetail", exception.Code);
    }

    [Fact]
    public void TotalQuestions_IsDerivedFromDetails_TotalScoreIsStoredDirectly()
    {
        var matrix = DirectDraft();
        matrix.TotalScore = 10;
        matrix.ReplaceDetails(new[]
        {
            Detail(questionCount: 2, percentage: 37.5m),
            Detail(lessonId: 2, questionCount: 3, percentage: 62.5m)
        }, Pht);

        Assert.Equal((uint)5, matrix.TotalQuestions);
        // TotalScore is set directly by the creator now, not derived from the detail rows.
        Assert.Equal(10, matrix.TotalScore);
        Assert.Equal(100m, matrix.Details.Sum(detail => detail.Percentage));
    }

    [Theory]
    [InlineData(37.5, 37.5)]
    [InlineData(60, 60)]
    [InlineData(1, 1)]
    public void ReplaceDetails_AcceptsAnyPercentageTotalOnADraft(double first, double second)
    {
        var matrix = DirectDraft();

        matrix.ReplaceDetails(new[]
        {
            Detail(percentage: (decimal)first),
            Detail(lessonId: 2, percentage: (decimal)second)
        }, Pht);

        Assert.Equal((decimal)first + (decimal)second, matrix.Details.Sum(detail => detail.Percentage));
    }

    [Fact]
    public void ReplaceDetails_AcceptsAnEmptyDraft()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail() }, Pht);

        matrix.ReplaceDetails(Array.Empty<MatrixDetailValue>(), Pht);

        Assert.Empty(matrix.Details);
    }

    [Theory]
    [InlineData(37.5, 37.5)]
    [InlineData(60, 60)]
    public void Submit_RejectsATotalThatIsNotExactlyOneHundredPercent(double first, double second)
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[]
        {
            Detail(percentage: (decimal)first),
            Detail(lessonId: 2, percentage: (decimal)second)
        }, Pht);

        var error = Assert.Throws<MatrixDomainException>(() => matrix.Submit(Pht));

        Assert.Equal("InvalidTotalScore", error.Code);
        Assert.Equal(MatrixStatusCodes.Draft, matrix.Status);
    }

    [Fact]
    public void ConfirmDirect_RejectsATotalThatIsNotExactlyOneHundredPercent()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail(percentage: 90m) }, Pht);

        var error = Assert.Throws<MatrixDomainException>(() => matrix.ConfirmDirect(Pht));

        Assert.Equal("InvalidTotalScore", error.Code);
        Assert.Equal(MatrixStatusCodes.Draft, matrix.Status);
    }

    [Fact]
    public void ReplaceDetails_OnASubmittedMatrixMustKeepTheTotalAtOneHundredPercent()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail(lessonId: 9) }, Pht);
        matrix.Submit(Pht);

        var error = Assert.Throws<MatrixDomainException>(() =>
            matrix.ReplaceDetails(new[] { Detail(percentage: 90m) }, Pht));

        Assert.Equal("InvalidTotalScore", error.Code);
        // A rejected save must leave the previously stored details untouched.
        Assert.Equal(9UL, Assert.Single(matrix.Details).LessonId);
    }

    [Fact]
    public void Approve_RecordsWhoApprovedAndWhen()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;
        var at = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);

        matrix.Approve(Pht, at);

        Assert.Equal(Pht.UserId, matrix.ApprovedByUserId);
        Assert.Equal(at, matrix.ApprovedAt);
    }

    [Fact]
    public void ConfirmDirect_RecordsWhoApprovedAndWhen()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail() }, Pht);
        var at = new DateTime(2026, 9, 21, 9, 0, 0, DateTimeKind.Utc);

        matrix.ConfirmDirect(Pht, at);

        Assert.Equal(Pht.UserId, matrix.ApprovedByUserId);
        Assert.Equal(at, matrix.ApprovedAt);
    }

    [Fact]
    public void Reject_LeavesNoApprover()
    {
        var matrix = DelegatedDraft();
        matrix.Status = MatrixStatusCodes.Submitted;

        matrix.Reject(Pht, "x", DateTime.UtcNow);

        Assert.Null(matrix.ApprovedByUserId);
        Assert.Null(matrix.ApprovedAt);
    }

    [Fact]
    public void CloneAsDraft_IsAuthoredByWhoeverMadeTheCopy()
    {
        var original = DirectDraft();
        original.ReplaceDetails(new[] { Detail() }, Pht);
        original.CreatedByUserId = 99;
        original.ConfirmDirect(Pht);
        var at = new DateTime(2026, 9, 22, 1, 0, 0, DateTimeKind.Utc);

        var clone = original.CloneAsDraft(Pht, at);

        Assert.Equal(Pht.UserId, clone.CreatedByUserId);
        Assert.Equal(at, clone.CreatedAt);
        Assert.Null(clone.ApprovedByUserId);
        Assert.Null(clone.ApprovedAt);
        // The original keeps its own author and approver.
        Assert.Equal(99UL, original.CreatedByUserId);
        Assert.Equal(Pht.UserId, original.ApprovedByUserId);
    }

    [Fact]
    public void HasRequiredTotalPercentage_IsTrueOnlyForExactlyOneHundred()
    {
        var matrix = DirectDraft();
        matrix.ReplaceDetails(new[] { Detail(percentage: 99.99m) }, Pht);
        Assert.False(matrix.HasRequiredTotalPercentage);

        matrix.ReplaceDetails(new[] { Detail(percentage: 100m) }, Pht);
        Assert.True(matrix.HasRequiredTotalPercentage);
    }

    [Fact]
    public void ReplaceDetails_AcceptsExactlyOneHundredPercentSplitAcrossSeveralCells()
    {
        var matrix = DirectDraft();

        matrix.ReplaceDetails(new[]
        {
            Detail(cognitiveLevel: "NHAN_BIET", percentage: 37.5m),
            Detail(cognitiveLevel: "THONG_HIEU", percentage: 37.5m),
            Detail(cognitiveLevel: "VAN_DUNG", percentage: 25m)
        }, Pht);

        Assert.Equal(100m, matrix.Details.Sum(detail => detail.Percentage));
    }

    [Fact]
    public void CloneCreatesUnlinkedDraftWithCopiedDetails()
    {
        var matrix = DirectDraft();
        matrix.Id = 42;
        matrix.TotalScore = 10;
        matrix.ReplaceDetails(new[] { Detail() }, Pht);
        matrix.Status = MatrixStatusCodes.Archived;

        var clone = matrix.CloneAsDraft(Pht);

        Assert.Equal((ulong)0, clone.Id);
        Assert.Equal(MatrixStatusCodes.Draft, clone.Status);
        Assert.Null(clone.TaskId);
        Assert.Equal(matrix.Name, clone.Name);
        Assert.Equal(matrix.TotalScore, clone.TotalScore);
        Assert.Single(clone.Details);
        Assert.Equal(matrix.Details.Single().QuestionCount, clone.Details.Single().QuestionCount);
        Assert.Equal(matrix.Details.Single().Percentage, clone.Details.Single().Percentage);
    }

    [Fact]
    public void ArchiveRequiresApprovedStatusAndPhtActor()
    {
        var matrix = DirectDraft();

        Assert.Throws<MatrixDomainException>(() => matrix.Archive(Pht));

        matrix.Status = MatrixStatusCodes.Approved;
        matrix.Archive(Pht);

        Assert.Equal(MatrixStatusCodes.Archived, matrix.Status);
    }

    private static ExamMatrix DirectDraft()
    {
        return new ExamMatrix
        {
            Id = 0,
            Name = "Direct matrix",
            Status = MatrixStatusCodes.Draft,
            TaskId = null,
            AcademicContextId = 100
        };
    }

    private static ExamMatrix DelegatedDraft(ulong assignedToUserId = 20)
    {
        return new ExamMatrix
        {
            Id = 0,
            Name = "Delegated matrix",
            Status = MatrixStatusCodes.Draft,
            TaskId = 7,
            AcademicContextId = 100,
            Task = new WorkTask
            {
                Id = 7,
                AssignedToUserId = assignedToUserId,
                TaskType = "MATRIX"
            }
        };
    }

    private static MatrixDetailValue Detail(
        ulong lessonId = 1,
        string cognitiveLevel = "NHAN_BIET",
        string questionType = "MULTIPLE_CHOICE",
        uint questionCount = 1,
        decimal percentage = ExamMatrix.RequiredTotalPercentage)
    {
        return new MatrixDetailValue(
            lessonId,
            cognitiveLevel,
            questionType,
            questionCount,
            percentage);
    }
}
