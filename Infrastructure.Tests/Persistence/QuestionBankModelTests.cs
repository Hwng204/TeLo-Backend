using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public sealed class QuestionBankModelTests
{
    [Fact]
    public void Model_MapsQuestionBankAndMatrixTables()
    {
        var expected = new[]
        {
            "exam_matrices", "exam_set_questions", "exam_sets", "exam_variant_questions",
            "exam_variants", "matrix_details", "question_banks", "question_options",
            "question_task_details", "question_tasks", "questions", "tasks"
        };

        var actual = ModelFactory.CreateModel().GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(table => table is not null && expected.Contains(table))
            .OrderBy(table => table)
            .ToArray();

        Assert.Equal(expected.OrderBy(table => table), actual);
    }

    [Fact]
    public void MatrixAndQuestionFields_UseApprovedTypesAndUniqueCells()
    {
        var model = ModelFactory.CreateModel();
        var detail = model.FindEntityType(typeof(MatrixDetail))!;
        var variantQuestion = model.FindEntityType(typeof(ExamVariantQuestion))!;

        Assert.Equal("decimal(5,2)", detail.FindProperty(nameof(MatrixDetail.Percentage))!.GetColumnType());
        Assert.Equal("json", variantQuestion.FindProperty(nameof(ExamVariantQuestion.OptionOrderJson))!.GetColumnType());
        Assert.Contains(detail.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "uq_matrix_details_cell" &&
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[]
            {
                "exam_matrix_id", "lesson_id", "cognitive_level", "question_type"
            }));
    }

    [Fact]
    public void TaskAndQuestionRelationships_PreserveDeleteBehavior()
    {
        var model = ModelFactory.CreateModel();
        var matrix = model.FindEntityType(typeof(ExamMatrix))!;
        var question = model.FindEntityType(typeof(Question))!;

        Assert.Contains(matrix.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "task_id" }) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(question.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "question_bank_id" }) &&
            foreignKey.DeleteBehavior == DeleteBehavior.SetNull);
    }

    [Fact]
    public void MatrixTaskScopeAndDirectMatrixLink_AreOptionalAndIndexed()
    {
        var model = ModelFactory.CreateModel();
        var matrix = model.FindEntityType(typeof(ExamMatrix))!;
        var task = model.FindEntityType(typeof(WorkTask))!;

        var matrixTaskId = matrix.FindProperty(nameof(ExamMatrix.TaskId))!;
        Assert.True(matrixTaskId.IsNullable);
        Assert.Equal(typeof(ulong?), matrixTaskId.ClrType);
        Assert.Contains(matrix.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "task_id" }) &&
            !foreignKey.IsRequired);

        Assert.True(task.FindProperty(nameof(WorkTask.AcademicContextId))!.IsNullable);
        Assert.True(task.FindProperty(nameof(WorkTask.SemesterId))!.IsNullable);
        Assert.Contains(task.GetIndexes(), index =>
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "academic_context_id" }) &&
            index.GetDatabaseName() == "idx_tasks_context");
        Assert.Contains(task.GetIndexes(), index =>
            index.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "semester_id" }) &&
            index.GetDatabaseName() == "idx_tasks_semester");
        Assert.Contains(task.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "academic_context_id" }) &&
            foreignKey.GetConstraintName() == "fk_tasks_context");
        Assert.Contains(task.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.GetColumnName()).SequenceEqual(new[] { "semester_id" }) &&
            foreignKey.GetConstraintName() == "fk_tasks_semester");

        var detail = ModelFactory.CreateDesignModel().FindEntityType(typeof(MatrixDetail))!;
        var percentageConstraint = detail.GetCheckConstraints().Single(constraint =>
            constraint.Name == "ck_matrix_details_percentage");
        Assert.Equal("percentage > 0 AND percentage <= 100", percentageConstraint.Sql);
    }
}
