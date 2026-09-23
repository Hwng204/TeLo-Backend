using Domain.Entities.Examination;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class ExaminationModelTests
{
    [Fact]
    public void Model_MapsExaminationTables()
    {
        var expected = new[]
        {
            "exam_attempt_answers", "exam_attempts", "exam_proctors", "exam_registrations",
            "exam_rooms", "exam_sessions", "exam_subject_grade_levels", "exam_subjects",
            "exams", "proctor_assignments", "session_rooms", "technical_incidents", "violations"
        };

        var actual = ModelFactory.CreateModel().GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && expected.Contains(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.Equal(expected.OrderBy(table => table), actual);
    }

    [Fact]
    public void ExaminationFields_UseApprovedTypesAndIndexes()
    {
        var model = ModelFactory.CreateModel();
        var session = model.FindEntityType(typeof(ExamSession))!;
        var attempt = model.FindEntityType(typeof(ExamAttempt))!;
        var registration = model.FindEntityType(typeof(ExamRegistration))!;

        Assert.Equal("datetime(6)", session.FindProperty(nameof(ExamSession.StartAt))!.GetColumnType());
        Assert.Equal("decimal(5,2)", attempt.FindProperty(nameof(ExamAttempt.TotalScore))!.GetColumnType());
        Assert.Contains(registration.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "uq_exam_registrations_scope_student" &&
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[]
            {
                "exam_subject_grade_level_id", "student_id"
            }));
    }

    [Fact]
    public void ExaminationRelationships_PreserveCascadeAndSetNullBehavior()
    {
        var model = ModelFactory.CreateModel();
        var subject = model.FindEntityType(typeof(ExamSubject))!;
        var answer = model.FindEntityType(typeof(ExamAttemptAnswer))!;

        Assert.Contains(subject.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "exam_id" }) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(answer.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "selected_option_id" }) &&
            foreignKey.DeleteBehavior == DeleteBehavior.SetNull);
    }
}
