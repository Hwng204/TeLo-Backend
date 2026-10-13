using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WebAPI.Tests;

public sealed class QuestionPracticeIntegrationTests
{
    [IdentityMySqlFact]
    public async Task CompletedExam_ReleasesOnlyUsedQuestions_AfterFutureExamProtectionEnds()
    {
        await using var f = await TasksIntegrationTests.TaskFixture.CreateAsync();
        var task = new WorkTask { TaskType = "QUESTION", Name = "Practice test", Status = "ASSIGNED", AssignedToUserId = f.Id("gv1"), CreatedByUserId = f.Id("tl") };
        var group = new QuestionTask { Task = task, LessonId = f.LessonA1.Id, Status = "ASSIGNED", AssignedQuestionCount = 2 };
        group.Details.Add(new() { CognitiveLevel = "NHAN_BIET", QuestionType = "MULTIPLE_CHOICE", QuestionCount = 2 }); f.Db.QuestionTasks.Add(group); await f.Db.SaveChangesAsync();
        var teacher = new QuestionActor(f.Id("gv1"), true, false, false, false); var lead = new QuestionActor(f.Id("tl"), false, true, false, false);
        var service = new QuestionService(f.Db, TimeProvider.System);
        await service.SaveAsync(teacher, task.Id, null, new(0, group.Details.Single().Id, "Câu thực sự đã thi", ["1", "2", "3", "4"], "B", null), default);
        await service.SaveAsync(teacher, task.Id, null, new(1, group.Details.Single().Id, "Câu dự phòng chưa dùng", ["1", "2", "3", "4"], "B", null), default);
        await service.SubmitAsync(teacher, task.Id, 2, default); var approved = await service.ReviewAsync(lead, task.Id, true, new(3, null, null), default);
        var usedId = approved.Questions[0].Id; var backupId = approved.Questions[1].Id;
        var main = new ExamSet { Name = "Đề đã thi", Status = "APPROVED", Purpose = "exam", CreatedByUserId = f.Id("tl"), ApprovedByUserId = f.Id("pht"), ApprovedAt = DateTime.UtcNow };
        var mainQuestion = new ExamSetQuestion { ExamSet = main, QuestionId = usedId }; main.Questions.Add(mainQuestion);
        var backup = new ExamSet { Name = "Đề dự phòng", Status = "APPROVED", Purpose = "exam", CreatedByUserId = f.Id("tl"), ApprovedByUserId = f.Id("pht"), ApprovedAt = DateTime.UtcNow };
        backup.Questions.Add(new() { QuestionId = backupId });
        Exam NewExam(string status) => new() { SemesterId = f.SemesterA.Id, SchoolBranchId = f.ContextA.SchoolBranchId, Name = status, Status = status, StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), StartTime = new(8, 0), EndTime = new(9, 0) };
        var completed = NewExam("COMPLETED"); var future = NewExam("SCHEDULED");
        ExamSubjectGradeLevel Grade(Exam exam) => new() { ExamSubject = new() { Exam = exam, SubjectId = f.ContextA.SubjectId, DurationMinutes = 30, Status = "ACTIVE" }, GradeLevelId = f.ContextA.GradeLevelId, PrimaryExamSet = main, BackupExamSet = backup, Status = "ACTIVE" };
        var completedGrade = Grade(completed); var futureGrade = Grade(future);
        var nextSet = new ExamSet { Name = "Đề kỳ thi tiếp theo", Status = "APPROVED", Purpose = "exam", CreatedByUserId = f.Id("tl"), ApprovedByUserId = f.Id("pht"), ApprovedAt = DateTime.UtcNow };
        nextSet.Questions.Add(new() { QuestionId = usedId });
        futureGrade.PrimaryExamSet = nextSet; futureGrade.BackupExamSet = null;
        var variant = new ExamVariant { ExamSet = main, VariantCode = "A" }; variant.Questions.Add(new() { ExamSetQuestion = mainQuestion, PositionNo = 1 });
        var registration = new ExamRegistration { ExamSubjectGradeLevel = completedGrade, Student = new Student { Code = "QUESTION_TEST", FullName = "Học sinh thử" }, Status = "ACTIVE" };
        f.Db.AddRange(new ExamAttempt { ExamRegistration = registration, ExamVariant = variant, StartedAt = DateTime.UtcNow.AddDays(-3), SubmittedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(20), Status = "SUBMITTED" }, futureGrade);
        await f.Db.SaveChangesAsync();
        var bank = new QuestionBankService(f.Db, service, TimeProvider.System);
        Assert.Equal(0, await bank.ReleaseCompletedAsync(default));
        future.Status = "CANCELLED"; await f.Db.SaveChangesAsync();
        var optionIds = await f.Db.QuestionOptions.Where(x => x.QuestionId == usedId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        Assert.Equal(1, await bank.ReleaseCompletedAsync(default)); Assert.Equal(0, await bank.ReleaseCompletedAsync(default));
        Assert.Equal("practice", await f.Db.Questions.Where(x => x.Id == usedId).Select(x => x.QuestionBank!.BankType).SingleAsync());
        Assert.Equal("exam", await f.Db.Questions.Where(x => x.Id == backupId).Select(x => x.QuestionBank!.BankType).SingleAsync());
        Assert.Equal(usedId, await f.Db.ExamSetQuestions.Where(x => x.ExamSetId == main.Id).Select(x => x.QuestionId).SingleAsync());
        Assert.Equal(optionIds, await f.Db.QuestionOptions.Where(x => x.QuestionId == usedId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
    }
}
