using Domain.Entities.QuestionBank;
using Xunit;

namespace Application.Tests;

public sealed class MatrixReviewerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TaskMatrix_OnlyAssignedReviewerCanReview(bool reject)
    {
        var matrix = new ExamMatrix
        {
            Status = MatrixStatusCodes.Submitted, TaskId = 10,
            Task = new WorkTask { CreatedByUserId = 1, AssignedToUserId = 3 }
        };
        var unrelated = new MatrixActor(2, MatrixActorRole.Pht);
        Assert.False(matrix.CanEdit(unrelated));
        var error = Assert.Throws<MatrixDomainException>(() =>
        {
            if (reject) matrix.Reject(unrelated);
            else matrix.Approve(unrelated);
        });
        Assert.Equal("Forbidden", error.Code);
        Assert.Equal(MatrixStatusCodes.Submitted, matrix.Status);
        var reviewer = new MatrixActor(1, MatrixActorRole.Pht);
        Assert.True(matrix.CanEdit(reviewer));
        if (reject) matrix.Reject(reviewer);
        else matrix.Approve(reviewer);
        Assert.Equal(reject ? MatrixStatusCodes.Draft : MatrixStatusCodes.Approved, matrix.Status);
    }

    // A direct matrix has no reviewer column: only its author approves or confirms it.
    [Fact]
    public void DirectMatrix_OnlyItsAuthorCanApproveOrConfirm()
    {
        var other = new MatrixActor(2, MatrixActorRole.Pht);
        var author = new MatrixActor(1, MatrixActorRole.Pht);
        var submitted = new ExamMatrix { Status = MatrixStatusCodes.Submitted, CreatedByUserId = 1 };
        Assert.Equal("Forbidden", Assert.Throws<MatrixDomainException>(() => submitted.Approve(other)).Code);
        Assert.Equal(MatrixStatusCodes.Submitted, submitted.Status);
        submitted.Approve(author);
        Assert.Equal(MatrixStatusCodes.Approved, submitted.Status);

        var draft = new ExamMatrix { Status = MatrixStatusCodes.Draft, CreatedByUserId = 1 };
        draft.Details.Add(new MatrixDetail { LessonId = 5, QuestionCount = 2, Percentage = 100 });
        Assert.Equal("Forbidden", Assert.Throws<MatrixDomainException>(() => draft.ConfirmDirect(other)).Code);
        Assert.Equal(MatrixStatusCodes.Draft, draft.Status);
        draft.ConfirmDirect(author);
        Assert.Equal(MatrixStatusCodes.Approved, draft.Status);
        Assert.Equal(1UL, draft.ApprovedByUserId);
    }
}
