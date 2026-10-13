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
            if (reject) matrix.Reject(unrelated, "Cần sửa", DateTime.UtcNow);
            else matrix.Approve(unrelated);
        });
        Assert.Equal("Forbidden", error.Code);
        Assert.Equal(MatrixStatusCodes.Submitted, matrix.Status);
        var reviewer = new MatrixActor(1, MatrixActorRole.Pht);
        Assert.True(matrix.CanEdit(reviewer));
        if (reject) matrix.Reject(reviewer, "Cần sửa", DateTime.UtcNow);
        else matrix.Approve(reviewer);
        Assert.Equal(reject ? MatrixStatusCodes.Draft : MatrixStatusCodes.Approved, matrix.Status);
    }

    [Fact]
    public void DirectMatrix_StillAllowsVicePrincipalReview()
    {
        var matrix = new ExamMatrix { Status = MatrixStatusCodes.Submitted, CreatedByUserId = 1 };
        matrix.Approve(new MatrixActor(2, MatrixActorRole.Pht));
        Assert.Equal(MatrixStatusCodes.Approved, matrix.Status);
    }
}
