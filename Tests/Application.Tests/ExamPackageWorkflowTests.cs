using Application.Common;
using Xunit;

namespace Application.Tests;

public sealed class ExamPackageWorkflowTests
{
    [Fact]
    public void TeacherCanSubmitButOnlyPhtCanFinalizeReviewedExam()
    {
        Assert.True(Allowed("REVIEW_ASSIGNED", "DRAFT", manager: true, pht: true));
        Assert.True(Allowed("QUESTION_REPLACED", "IN_REVIEW", reviewer: true));
        Assert.True(Allowed("REVIEW_SUBMITTED", "IN_REVIEW", reviewer: true));
        Assert.False(Allowed("FINALIZED", "REVIEWED", reviewer: true));
        Assert.False(Allowed("FINALIZED", "REVIEWED", manager: true));
        Assert.True(Allowed("FINALIZED", "REVIEWED", pht: true));
        Assert.False(Allowed("QUESTION_REPLACED", "APPROVED", reviewer: true));
        Assert.False(Allowed("RENAMED", "APPROVED", manager: true, pht: true));
    }

    [Fact]
    public void ReturningReopensReviewWithoutAllowingEarlyFinalize()
    {
        Assert.True(Allowed("REVIEW_RETURNED", "REVIEWED", pht: true));
        Assert.False(Allowed("REVIEW_RETURNED", "IN_REVIEW", pht: true));
        Assert.False(Allowed("FINALIZED", "IN_REVIEW", pht: true));
        Assert.True(Allowed("REVIEW_SUBMITTED", "IN_REVIEW", reviewer: true));
    }

    [Theory]
    [InlineData("RENAMED")]
    [InlineData("REVIEW_ASSIGNED")]
    [InlineData("DELETED")]
    public void BeingTheOriginalCreatorDoesNotGiveATeamLeadPhtManagementRights(string action)
    {
        Assert.False(Allowed(action, "DRAFT", manager: true));
        Assert.True(Allowed(action, "DRAFT", manager: true, pht: true));
    }

    [Fact]
    public void UnassignedTeacherAndPracticeCopyCannotMutateExamWorkflow()
    {
        Assert.False(Allowed("QUESTION_REPLACED", "IN_REVIEW"));
        Assert.False(Allowed("REVIEW_SUBMITTED", "IN_REVIEW"));
        foreach (var action in new[] { "RENAMED", "REVIEW_ASSIGNED", "FINALIZED", "PRACTICE_COPIED", "DELETED" })
            Assert.False(ExamPackageWorkflow.Allowed(action, "APPROVED", "practice", true, true, true));
    }

    private static bool Allowed(string action, string status, bool manager = false, bool reviewer = false, bool pht = false) =>
        ExamPackageWorkflow.Allowed(action, status, "exam", manager, reviewer, pht);
}
