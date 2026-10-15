using Application.Common;
using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests;

public sealed class ExamPackageV2RulesTests
{
    [Fact]
    public void GenerationUsesExactCellAndScopeWithoutDuplicateQuestions()
    {
        var matrix = Matrix();
        var questions = new[] { Question(1), Question(2), Question(3), Question(4) };
        var selected = ExamPackageRules.Select(matrix, questions);
        Assert.Equal(2, selected.Count);
        Assert.Equal(2, selected.Select(x => x.Question.Id).Distinct().Count());
        ExamPackageRules.Validate(matrix, selected);
    }

    [Theory]
    [InlineData("branch")]
    [InlineData("subject")]
    [InlineData("grade")]
    [InlineData("lesson")]
    [InlineData("level")]
    [InlineData("type")]
    [InlineData("practice")]
    [InlineData("draft")]
    [InlineData("missing-option")]
    public void MismatchedOrIncompleteBankQuestionsCannotEnterExam(string mismatch)
    {
        var question = Question(1);
        switch (mismatch)
        {
            case "branch": question.Lesson.Chapter.SchoolBranchId++; break;
            case "subject": question.Lesson.Chapter.SubjectId++; break;
            case "grade": question.Lesson.Chapter.GradeLevelId++; break;
            case "lesson": question.QuestionTaskDetail.QuestionTask.LessonId++; break;
            case "level": question.QuestionTaskDetail.CognitiveLevel = "VAN_DUNG"; break;
            case "type": question.QuestionTaskDetail.QuestionType = "OTHER"; break;
            case "practice": question.BankType = "practice"; break;
            case "draft": question.Status = "DRAFT"; break;
            case "missing-option": question.Options.First().Content = ""; break;
        }
        Assert.False(ExamPackageRules.Matches(question, Matrix(), Matrix().Details.Single()));
    }

    [Fact]
    public void ShortageFailsTheWholeGenerationAndDoesNotMutateCandidates()
    {
        var question = Question(1);
        var error = Assert.Throws<QuestionException>(() => ExamPackageRules.Select(Matrix(), [question]));
        Assert.Equal("EXAM_QUESTIONS_SHORTAGE", error.Code);
        Assert.Equal("APPROVED", question.Status);
        Assert.Equal("exam", question.BankType);
    }

    [Fact]
    public void JournalIndexFiltersTranslateBeforePaginationWithoutDatabaseConnection()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=unused;User=unused;Password=unused;", new MySqlServerVersion(new Version(8, 0, 0))).Options);
        var sql = ExamPackageIndex.Filter(db.ExamSets.Where(x => x.ExamMatrix.BranchId == 1), db, "practice", 2)
            .OrderBy(x => x.Id).Skip(10).Take(10).ToQueryString();
        Assert.Contains("task_history", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("branch_id", sql);
    }

    private static ExamMatrix Matrix() => new()
    {
        Id = 1, BranchId = 1, SubjectId = 2, GradeLevelId = 3, TotalScore = 10, Status = "APPROVED",
        Details = [new() { Id = 7, LessonId = 4, CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 2, Percentage = 100 }]
    };

    private static Question Question(ulong id) => new()
    {
        Id = id, LessonId = 4, BankType = "exam", Status = "APPROVED", Content = "Nội dung",
        Lesson = new() { Id = 4, Chapter = new() { SchoolBranchId = 1, SubjectId = 2, GradeLevelId = 3 } },
        QuestionTaskDetail = new() { CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionTask = new() { LessonId = 4 } },
        Options = [new() { OptionKey = "A", Content = "A", IsCorrect = true }, new() { OptionKey = "B", Content = "B" },
            new() { OptionKey = "C", Content = "C" }, new() { OptionKey = "D", Content = "D" }]
    };
}
