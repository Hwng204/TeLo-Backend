using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class QuestionRulesTests
{
    private static SaveQuestionRequest Valid() => new(0, 1, "2 + 3 = ?", ["4", "5", "6", "7"], "B", null);

    [Fact]
    public void DraftMayBeIncompleteButCannotBeSubmitted()
    {
        var draft = Valid() with { Content = "", CorrectOption = null };
        Assert.Empty(QuestionRules.DraftErrors(draft));
        Assert.False(QuestionRules.IsComplete(draft.Content, draft.Options, draft.CorrectOption));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("B", true)]
    [InlineData("E", false)]
    [InlineData(null, false)]
    public void CompleteQuestionRequiresOneOfFourAnswers(string? answer, bool complete) =>
        Assert.Equal(complete, QuestionRules.IsComplete(Valid().Content, Valid().Options, answer));

    [Fact]
    public void UnusedChoiceIsAllowedButDuplicateUsedChoicesAreNotComplete()
    {
        Assert.True(QuestionRules.IsComplete("Câu hỏi", ["1", "2", " ", "4"], "A"));
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["1", " 1 ", "3", "4"], "A"));
    }

    [Fact]
    public void CapitalizationQuestionsMayUseCaseSensitiveChoices() =>
        Assert.True(QuestionRules.IsComplete("Cách viết hoa nào đúng?", ["Hà Nội", "hà nội", "Hà nội", "hà Nội"], "A"));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SingleAnswerQuestionAcceptsTwoToFourChoices(int count)
    {
        var options = Enumerable.Range(1, 4).Select(x => x <= count ? x.ToString() : "").ToArray();
        var request = Valid() with { Options = options };
        Assert.Empty(QuestionRules.DraftErrors(request));
        Assert.True(QuestionRules.IsComplete(request.Content, options, "B"));
    }

    [Fact]
    public void AnyChoiceMayBeUnusedButAnswerMustPointToAUsedChoice()
    {
        Assert.True(QuestionRules.IsComplete("Câu hỏi", ["1", "2", "", " "], "B"));
        Assert.True(QuestionRules.IsComplete("Câu hỏi", ["1", "2", "3", ""], "C"));
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["1", "2", "", ""], "C"));
        Assert.True(QuestionRules.IsComplete("Câu hỏi", ["1", "", "3", ""], "C"));
        Assert.True(QuestionRules.IsComplete("Câu hỏi", ["", "2", "", "4"], "D"));
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["1", "", "", ""], "A"));
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["", "", "", ""], "A"));
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["1", "1", "", ""], "A"));
    }

    [Fact]
    public void ChoiceCountAndNullValuesAreValidatedAtTheBoundary()
    {
        Assert.Contains("options", QuestionRules.DraftErrors(Valid() with { Options = ["1"] }).Keys);
        Assert.Contains("options", QuestionRules.DraftErrors(Valid() with { Options = ["1", "2", "3", "4", "5"] }).Keys);
        Assert.Contains("options", QuestionRules.DraftErrors(Valid() with { Options = ["1", null!] }).Keys);
        Assert.False(QuestionRules.IsComplete("Câu hỏi", ["1", null!], "A"));
    }

    [Fact]
    public void EqualTotalWithWrongDistributionCannotSubmit()
    {
        Assert.False(QuestionRules.Ready([(2, 1), (1, 2)]));
        Assert.True(QuestionRules.Ready([(2, 2), (1, 1)]));
        Assert.False(QuestionRules.Ready([]));
    }

    [Theory]
    [InlineData("ASSIGNED", true)]
    [InlineData("IN_PROGRESS", true)]
    [InlineData("NEEDS_REVISION", true)]
    [InlineData("PENDING_REVIEW", false)]
    [InlineData("COMPLETED", false)]
    public void PendingAndCompletedPackagesAreImmutable(string status, bool editable) =>
        Assert.Equal(editable, QuestionRules.Editable(status));

    [Fact]
    public void DraftRejectsUnboundedTextAndUnknownAnswer()
    {
        Assert.Contains("content", QuestionRules.DraftErrors(Valid() with { Content = new string('a', 10001) }).Keys);
        Assert.Contains("correctOption", QuestionRules.DraftErrors(Valid() with { CorrectOption = "E" }).Keys);
    }
}
