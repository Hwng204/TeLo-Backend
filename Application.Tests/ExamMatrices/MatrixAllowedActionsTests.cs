using Application.Mappings;
using Domain.Entities.QuestionBank;

namespace Application.Tests.ExamMatrices;

/// <summary>
/// Submit and Confirm are offered only when the matrix's rows add up to exactly 100%, so the UI
/// never shows a button the domain would reject with InvalidTotalScore.
/// </summary>
public sealed class MatrixAllowedActionsTests
{
    private static readonly MatrixActor Pht = new(10, MatrixActorRole.Pht, 1);

    [Theory]
    [InlineData(99.99, false)]
    [InlineData(100.01, false)]
    [InlineData(100, true)]
    public void DirectDraft_OffersSubmitAndConfirmOnlyAtExactlyOneHundredPercent(double totalPercentage, bool offered)
    {
        var matrix = DraftWithTotal((decimal)totalPercentage);

        var actions = matrix.ToResponse(Pht).AllowedActions;

        Assert.Equal(offered, actions.Contains("Confirm"));
        Assert.Equal(offered, actions.Contains("Submit"));
        // Editing and deleting stay available at any total so the draft can be finished later.
        Assert.Contains("Update", actions);
        Assert.Contains("Delete", actions);
    }

    [Fact]
    public void EmptyDraft_OffersNeitherSubmitNorConfirm()
    {
        var matrix = new ExamMatrix { Id = 1, Name = "M", Status = MatrixStatusCodes.Draft };

        var actions = matrix.ToResponse(Pht).AllowedActions;

        Assert.DoesNotContain("Submit", actions);
        Assert.DoesNotContain("Confirm", actions);
        Assert.Contains("Update", actions);
    }

    private static ExamMatrix DraftWithTotal(decimal totalPercentage)
    {
        var matrix = new ExamMatrix { Id = 1, Name = "M", Status = MatrixStatusCodes.Draft, TotalScore = 10 };
        matrix.Details.Add(new MatrixDetail
        {
            LessonId = 1,
            CognitiveLevel = "NHAN_BIET",
            QuestionType = "MULTIPLE_CHOICE",
            QuestionCount = 1,
            Percentage = totalPercentage,
            ExamMatrix = matrix
        });
        return matrix;
    }
}
