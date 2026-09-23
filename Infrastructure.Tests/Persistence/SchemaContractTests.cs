using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Tests.Persistence;

public sealed class SchemaContractTests
{
    private static readonly string[] ExpectedTables =
    {
        "academic_contexts", "academic_years", "classes", "exam_attempt_answers", "exam_attempts",
        "exam_matrices", "exam_proctors", "exam_registrations", "exam_rooms", "exam_sessions",
        "exam_set_questions", "exam_sets", "exam_subject_grade_levels", "exam_subjects", "exam_variant_questions",
        "exam_variants", "exams", "grade_levels", "matrix_details", "modules", "navbars", "notification_configs",
        "notification_recipients", "notification_targets", "notifications", "permissions", "proctor_assignments", "provinces",
        "question_banks", "question_options", "question_task_details", "question_tasks", "questions", "roles", "rooms",
        "school_branches", "schools", "semesters", "session_rooms", "student_class_enrollments", "student_import_batches",
        "student_import_rows", "students", "subjects",
        "tasks", "teachers", "technical_incidents", "textbook_chapters", "textbook_lessons", "textbooks", "user_roles",
        "users", "violations"
    };

    [Fact]
    public void Model_HasExactlyTheFiftyThreeSchemaTables()
    {
        var model = ModelFactory.CreateModel();
        var actual = model.GetEntityTypes().Select(entity => entity.GetTableName()).OrderBy(table => table).ToArray();

        Assert.Equal(53, actual.Length);
        Assert.Equal(ExpectedTables.OrderBy(table => table), actual);
    }

    [Fact]
    public void Model_UsesSnakeCaseTableNamesAndSinglePrimaryKeyPerTable()
    {
        var model = ModelFactory.CreateModel();

        foreach (var entity in model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            Assert.NotNull(table);
            Assert.DoesNotContain(table!, character => char.IsUpper(character));
            Assert.NotNull(entity.FindPrimaryKey());
        }
    }

    [Fact]
    public void Model_ContainsTheTwentyEightApprovedRowChecksOnly()
    {
        var expected = new[]
        {
            "ck_academic_years_dates", "ck_academic_years_status", "ck_exam_attempt_answers_score", "ck_exam_attempts_score",
            "ck_exam_attempts_time", "ck_exam_matrices_total_score", "ck_exam_rooms_limit", "ck_exam_sessions_time",
            "ck_exam_subjects_duration", "ck_exam_variant_questions_position", "ck_exams_dates", "ck_matrix_details_count",
            "ck_matrix_details_percentage",
            "ck_notification_configs_recipient_scope", "ck_notification_configs_timing", "ck_notification_recipients_email_status",
            "ck_notification_recipients_sent_at", "ck_notification_targets_action", "ck_question_banks_type",
            "ck_question_task_details_count", "ck_question_tasks_count", "ck_semesters_dates", "ck_semesters_order",
            "ck_semesters_status", "ck_student_enrollments_status", "ck_student_import_batches_source",
            "ck_student_import_batches_status", "ck_students_status"
        };
        var actual = ModelFactory.CreateDesignModel().GetEntityTypes()
            .SelectMany(entity => entity.GetCheckConstraints())
            .Select(constraint => constraint.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name), actual);
    }
}
