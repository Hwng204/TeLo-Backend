using Application.Common;
using Application.DTOs;
using Domain.Entities.QuestionBank;
using Xunit;

namespace Application.Tests;

public sealed class TaskRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static DateTime Day(int offset) => Today.AddDays(offset).ToDateTime(TimeOnly.MinValue);

    private static SaveQuestionTaskRequest Question(
        string? name = "Biên soạn câu hỏi ôn tập",
        ulong performer = 1,
        int due = 1,
        IReadOnlyList<QuestionTaskLessonRequest>? lessons = null) =>
        new(name, performer, Day(due), lessons ?? [new(10, 8, 6, 4)]);

    private static SaveExamTaskRequest Exam(int paperCount = 2, int? due = 5) =>
        new("Tạo bộ đề cuối học kỳ I", 9, 3, due is null ? null : Day(due.Value), paperCount);

    private static CreateMatrixTaskRequest Matrix(int? due = null) =>
        new(3, 100, due is null ? null : Day(due.Value), "Ma trận Toán 5", null);

    [Fact]
    public void ValidRequestsHaveNoErrors()
    {
        Assert.Empty(TaskRules.QuestionTask(Question(), null, Today));
        Assert.Empty(TaskRules.ExamTask(Exam(), null, Today));
        Assert.Empty(TaskRules.MatrixTask(Matrix(), null, Today));
        Assert.Empty(TaskRules.MatrixTask(Matrix(due: 0), null, Today));
    }

    [Fact]
    public void QuestionTaskRequiresContentPerformerDeadlineAndLessons()
    {
        var errors = TaskRules.QuestionTask(new SaveQuestionTaskRequest(" ", 0, null, null), null, Today);

        // Không chọn người kiểm duyệt: Tổ trưởng giao việc kiểm duyệt.
        Assert.Equal(new[] { "assignedToUserId", "dueAt", "lessons", "name" }, errors.Keys.Order());
        Assert.Equal("Nội dung nhiệm vụ là bắt buộc.", errors["name"][0]);
        Assert.Equal("Thêm ít nhất một bài học.", errors["lessons"][0]);
    }

    [Fact]
    public void DeadlineCannotBeInThePast()
    {
        var past = TaskRules.QuestionTask(Question(due: -1), null, Today);

        Assert.Equal("Hạn biên soạn không được ở trong quá khứ.", Assert.Single(past["dueAt"]));
        Assert.Empty(TaskRules.QuestionTask(Question(due: 0), null, Today));
    }

    [Fact]
    public void EditingKeepsAnUnchangedOverdueDeadlineButRejectsANewPastOne()
    {
        var overdue = new WorkTask { DueAt = Day(-5) };

        Assert.Empty(TaskRules.QuestionTask(Question(due: -5), overdue, Today));
        Assert.Contains("dueAt", TaskRules.QuestionTask(Question(due: -4), overdue, Today).Keys);
        Assert.Contains("dueAt", TaskRules.MatrixTask(Matrix(due: -1), new WorkTask { DueAt = Day(-3) }, Today).Keys);
        Assert.Empty(TaskRules.MatrixTask(Matrix(due: -3), new WorkTask { DueAt = Day(-3) }, Today));
    }

    [Theory]
    [InlineData("duplicate", "Mỗi bài học chỉ được chọn một lần.")]
    [InlineData("empty", "Mỗi bài học cần ít nhất một câu hỏi.")]
    [InlineData("too-many-questions", "Mỗi mức nhận thức tối đa 1000 câu.")]
    [InlineData("no-lesson", "Chọn khối lớp, chương và bài học cho mọi thẻ bài học.")]
    [InlineData("too-many-lessons", "Một nhiệm vụ tối đa 50 bài học.")]
    public void LessonCardsNeedDistinctLessonsAndAtLeastOneQuestion(string kind, string message)
    {
        IReadOnlyList<QuestionTaskLessonRequest> lessons = kind switch
        {
            "duplicate" => [new(10, 1, 0, 0), new(10, 0, 2, 0)],
            "empty" => [new(10, 1, 0, 0), new(11, 0, 0, 0)],
            "too-many-questions" => [new(10, 1001, 0, 0)],
            "no-lesson" => [new(0, 1, 0, 0)],
            _ => Enumerable.Range(1, 51).Select(id => new QuestionTaskLessonRequest((ulong)id, 1, 0, 0)).ToArray()
        };

        var errors = TaskRules.QuestionTask(Question(lessons: lessons), null, Today);

        Assert.Equal(message, Assert.Single(errors["lessons"]));
    }

    [Fact]
    public void ContentIsTrimmedAndLimitedTo255Characters()
    {
        Assert.Empty(TaskRules.QuestionTask(Question(name: "  " + new string('a', 255) + "  "), null, Today));
        Assert.Equal("Nội dung nhiệm vụ tối đa 255 ký tự.",
            Assert.Single(TaskRules.QuestionTask(Question(name: new string('a', 256)), null, Today)["name"]));
        Assert.Equal("Nội dung nhiệm vụ là bắt buộc.",
            Assert.Single(TaskRules.MatrixTask(Matrix() with { Name = " " }, null, Today)["name"]));
    }

    [Fact]
    public void ExamTaskRequiresMatrixPerformerDeadlineAndPaperCount()
    {
        var errors = TaskRules.ExamTask(new SaveExamTaskRequest(null, 0, 0, null, 0), null, Today);

        // Không còn chọn kỳ thi: kỳ thi chọn đề sau, khi tạo kỳ thi.
        Assert.Equal(new[] { "assignedToUserId", "dueAt", "examMatrixId", "name", "paperCount" }, errors.Keys.Order());
        Assert.Equal("Hạn hoàn thành là bắt buộc.", errors["dueAt"][0]);
        Assert.Equal("Số đề cần tạo từ 1 đến 20.", errors["paperCount"][0]);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void PaperCountIsBetweenOneAndTwenty(int count, bool valid)
    {
        var errors = TaskRules.ExamTask(Exam(count), null, Today);

        if (valid) Assert.Empty(errors);
        else Assert.Equal("Số đề cần tạo từ 1 đến 20.", Assert.Single(errors["paperCount"]));
    }

    [Fact]
    public void PaperNamesAreNumberedBySystem()
    {
        Assert.Equal(new[] { "Đề 1", "Đề 2", "Đề 3" }, TaskRules.PaperNames(3));
        Assert.Equal(20, TaskRules.PaperNames(20).Count);
    }

    [Theory]
    [InlineData(MatrixTaskStatusCodes.Assigned, -1, true)]
    [InlineData(MatrixTaskStatusCodes.InProgress, -1, true)]
    [InlineData(MatrixTaskStatusCodes.NeedsRevision, -1, true)]
    [InlineData(MatrixTaskStatusCodes.NeedsRevision, 0, false)]
    [InlineData(MatrixTaskStatusCodes.Assigned, 0, false)]
    [InlineData(MatrixTaskStatusCodes.PendingReview, -3, false)]
    [InlineData(MatrixTaskStatusCodes.Completed, -3, false)]
    public void OverdueFollowsOnlyTheDueDate(string status, int due, bool overdue)
    {
        Assert.Equal(overdue, TaskRules.IsOverdue(status, Day(due), Today));
        Assert.False(TaskRules.IsOverdue(MatrixTaskStatusCodes.Assigned, null, Today));
    }

    [Fact]
    public void ListQueryAcceptsKnownTypesStatusesAndOverdue()
    {
        Assert.Empty(TaskRules.Query(new TaskListQuery(1, 10, "toán", TaskTypeCodes.Question, TaskRules.Overdue)));
        Assert.Empty(TaskRules.Query(new TaskListQuery(Status: MatrixTaskStatusCodes.PendingReview)));
        Assert.Empty(TaskRules.Query(new TaskListQuery(Status: MatrixTaskStatusCodes.NeedsRevision)));
        Assert.Equal(new[] { "page", "pageSize", "status", "taskType" },
            TaskRules.Query(new TaskListQuery(0, 101, null, "OTHER", "DONE")).Keys.Order());
        // "Đã nộp" đã gộp vào "Chờ kiểm duyệt".
        Assert.Contains("status", TaskRules.Query(new TaskListQuery(Status: "SUBMITTED")).Keys);
    }

    [Fact]
    public void LabelsMatchTheDesign()
    {
        Assert.Equal(new[] { "Ma trận", "Đề thi", "Câu hỏi" }, TaskTypeCodes.All.Select(TaskTypeCodes.Label));
        Assert.Equal(new[] { "Đã giao", "Đang thực hiện", "Chờ kiểm duyệt", "Cần chỉnh sửa", "Hoàn thành" },
            MatrixTaskStatusCodes.All.Select(MatrixTaskStatusCodes.Label));
    }
}
