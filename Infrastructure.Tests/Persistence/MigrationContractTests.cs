using Infrastructure.Context;
using Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Infrastructure.Tests.Persistence;

public sealed class MigrationContractTests
{
    [Fact]
    public void Migrations_AreInExpectedOrder()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=localhost;Database=sep_contract;User=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new ApplicationDbContext(options);

        var migrations = context.Database.GetMigrations().ToArray();

        Assert.Equal(15, migrations.Length);
        Assert.EndsWith("_InitialCreate", migrations[0]);
        Assert.EndsWith("_AddDatabaseIntegrityObjects", migrations[1]);
        Assert.EndsWith("_AddProvinceAcademicCalendarScope", migrations[2]);
        Assert.EndsWith("_AddTermLifecycleFields", migrations[3]);
        Assert.EndsWith("_AddProvinceCatalogSource", migrations[4]);
        Assert.EndsWith("_EnforceSingleActiveAcademicYear", migrations[5]);
        Assert.EndsWith("_AddImmutableAcademicYearCode", migrations[6]);
        Assert.EndsWith("_AddMatrixFeatureConstraints", migrations[7]);
        Assert.EndsWith("_AddMatrixRejection", migrations[8]);
        Assert.EndsWith("_AddClassStudentDirectoryReadModel", migrations[9]);
        Assert.EndsWith("_AddInactiveStudentStatus", migrations[10]);
        Assert.EndsWith("_AddStudentImportAndEnrollmentHistory", migrations[11]);
        Assert.EndsWith("_AddMatrixAuthorshipAndCode", migrations[12]);
        Assert.EndsWith("_AddMatrixPercentageScoring", migrations[13]);
        Assert.EndsWith("_RemoveMatrixCodeAddTaskName", migrations[14]);
    }

    [Fact]
    public void PercentageScoringMigration_BackfillsBeforeAddingConstraints_AndConvertsBackBeforeDroppingTotalScore()
    {
        var up = new ExposedPercentageScoringMigration().BuildUp().ToList();
        var down = new ExposedPercentageScoringMigration().BuildDown().ToList();

        // Up: total_score/percentage must be backfilled to valid values before the check
        // constraints that require them (>0, <=100) are added, or the migration errors on any
        // pre-existing row.
        var addTotalScoreColumn = up.FindIndex(operation =>
            operation is AddColumnOperation column && column.Table == "exam_matrices" && column.Name == "total_score");
        var backfillTotalScore = up.FindIndex(operation =>
            operation is SqlOperation sql && sql.Sql.Contains("SET total_score = 10"));
        var backfillPercentage = up.FindIndex(operation =>
            operation is SqlOperation sql && sql.Sql.Contains("SET percentage ="));
        var addPercentageCheck = up.FindIndex(operation =>
            operation is AddCheckConstraintOperation constraint && constraint.Name == "ck_matrix_details_percentage");
        var addTotalScoreCheck = up.FindIndex(operation =>
            operation is AddCheckConstraintOperation constraint && constraint.Name == "ck_exam_matrices_total_score");

        Assert.True(addTotalScoreColumn >= 0 && addTotalScoreColumn < backfillTotalScore);
        Assert.True(backfillTotalScore < addTotalScoreCheck);
        Assert.True(backfillPercentage >= 0 && backfillPercentage < addPercentageCheck);

        // Down: percentage must be converted back to an absolute score (using total_score) before
        // total_score itself is dropped, or the conversion would lose the multiplier it needs.
        var convertBack = down.FindIndex(operation =>
            operation is SqlOperation sql && sql.Sql.Contains("d.percentage = GREATEST"));
        var dropTotalScore = down.FindIndex(operation =>
            operation is DropColumnOperation column && column.Table == "exam_matrices" && column.Name == "total_score");

        Assert.True(convertBack >= 0 && convertBack < dropTotalScore);
    }

    [Fact]
    public void RemoveCodeMigration_DropsSequenceTable_AndRestoresAUniqueCodeBeforeReindexingOnDown()
    {
        var up = new ExposedRemoveCodeMigration().BuildUp().ToList();
        var down = new ExposedRemoveCodeMigration().BuildDown().ToList();

        // matrix_code_sequences was never an EF-mapped entity (created by raw SQL in
        // AddMatrixAuthorshipAndCode), so `migrations add` cannot see it on its own — this asserts
        // the by-hand DROP TABLE is actually there.
        Assert.Contains(up.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("DROP TABLE matrix_code_sequences", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(up.OfType<DropColumnOperation>(), operation =>
            operation.Table == "exam_matrices" && operation.Name == "code");
        Assert.Contains(up.OfType<AddColumnOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "name" && operation.IsNullable);

        // Down: AddColumn gives every row the same "" default; a per-row placeholder must be
        // backfilled before the unique index is recreated, or it fails on the very first duplicate.
        var addCodeColumn = down.FindIndex(operation =>
            operation is AddColumnOperation column && column.Table == "exam_matrices" && column.Name == "code");
        var backfillCode = down.FindIndex(operation =>
            operation is SqlOperation sql && sql.Sql.Contains("MT-RESTORED"));
        var recreateIndex = down.FindIndex(operation =>
            operation is CreateIndexOperation index && index.Name == "uq_exam_matrices_code");

        Assert.True(addCodeColumn >= 0 && addCodeColumn < backfillCode);
        Assert.True(backfillCode < recreateIndex);
    }

    [Fact]
    public void InitialCreate_CreatesFortyNineTablesAndCompositeNotificationParentFk()
    {
        var operations = new ExposedInitialCreate().BuildUp();

        Assert.Equal(49, operations.OfType<CreateTableOperation>().Count());
        var foreignKeys = operations.OfType<CreateTableOperation>()
            .SelectMany(operation => operation.ForeignKeys)
            .Concat(operations.OfType<AddForeignKeyOperation>())
            .ToArray();
        Assert.Equal(97, foreignKeys.Length);
        Assert.Contains(operations.OfType<AddForeignKeyOperation>(), operation =>
            operation.Name == "fk_notification_configs_base_match" &&
            operation.Table == "notification_configs" &&
            operation.PrincipalTable == "notification_configs" &&
            operation.Columns.SequenceEqual(new[] { "base_config_id", "school_id", "code" }) &&
            operation.PrincipalColumns!.SequenceEqual(new[] { "id", "school_id", "code" }));
        Assert.DoesNotContain(operations.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) ||
            operation.Sql.Contains("CREATE VIEW", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InitialCreate_UsesTinyIntForBooleanColumnsOnMySql8()
    {
        var operations = new ExposedInitialCreate().BuildUp();

        var booleanColumns = operations.OfType<CreateTableOperation>()
            .SelectMany(operation => operation.Columns)
            .Where(column => column.ClrType == typeof(bool) || column.ClrType == typeof(bool?))
            .ToArray();

        Assert.Equal(4, booleanColumns.Length);
        Assert.All(booleanColumns, column => Assert.Equal("tinyint(1)", column.ColumnType));
    }

    [Fact]
    public void IntegrityMigration_HasExactlyTwoGeneratedColumnsOneUniqueIndexAndElevenTriggers()
    {
        var migration = new ExposedIntegrityMigration();
        var up = migration.BuildUp();
        var down = migration.BuildDown().ToList();

        var generated = up.OfType<AddColumnOperation>()
            .Where(operation => operation.ComputedColumnSql is not null)
            .ToArray();
        Assert.Equal(2, generated.Length);
        Assert.Contains(generated, operation => operation.Name == "base_scope_school_key");
        Assert.Contains(generated, operation => operation.Name == "base_scope_code_key");

        var uniqueIndexes = up.OfType<CreateIndexOperation>()
            .Where(operation => operation.IsUnique)
            .ToArray();
        Assert.Single(uniqueIndexes);
        Assert.Equal("uq_notification_configs_base_scope", uniqueIndexes[0].Name);

        var triggerNames = up.OfType<SqlOperation>()
            .Select(operation => System.Text.RegularExpressions.Regex.Match(
                operation.Sql, @"CREATE TRIGGER\s+(?<name>[a-z0-9_]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups["name"].Value)
            .Where(name => name.Length > 0)
            .ToArray();
        var expectedTriggerNames = new[]
        {
            "trg_exam_sets_bi", "trg_exam_sets_ai", "trg_exam_sets_bu",
            "trg_exam_subjects_bi", "trg_exam_subjects_bu",
            "trg_exam_subject_grade_levels_bi", "trg_exam_subject_grade_levels_bu",
            "trg_notification_targets_bi", "trg_notification_targets_bu",
            "trg_notification_configs_bi", "trg_notification_configs_bu"
        };
        Assert.Equal(11, triggerNames.Length);
        Assert.Equal(11, triggerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(expectedTriggerNames.OrderBy(name => name), triggerNames.OrderBy(name => name));
        Assert.DoesNotContain(up.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("VIEW", StringComparison.OrdinalIgnoreCase));

        var firstTrigger = down.FindIndex(operation => operation is SqlOperation);
        var dropIndex = down.FindIndex(operation => operation is DropIndexOperation);
        var firstDropColumn = down.FindIndex(operation => operation is DropColumnOperation);
        Assert.True(firstTrigger >= 0 && firstTrigger < dropIndex && dropIndex < firstDropColumn);
        var droppedTriggerNames = down.OfType<SqlOperation>()
            .Select(operation => System.Text.RegularExpressions.Regex.Match(
                operation.Sql, @"DROP TRIGGER IF EXISTS\s+(?<name>[a-z0-9_]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups["name"].Value)
            .Where(name => name.Length > 0)
            .ToArray();
        Assert.Equal(expectedTriggerNames.Reverse(), droppedTriggerNames);
    }

    [Fact]
    public void MatrixFeatureMigration_ChangesOnlyMatrixScopeAndScoreContracts()
    {
        var migration = new ExposedMatrixMigration();
        var up = migration.BuildUp();
        var down = migration.BuildDown();

        Assert.Contains(up.OfType<AddColumnOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "academic_context_id" && operation.IsNullable);
        Assert.Contains(up.OfType<AddColumnOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "semester_id" && operation.IsNullable);
        Assert.Contains(up.OfType<AlterColumnOperation>(), operation =>
            operation.Table == "exam_matrices" && operation.Name == "task_id" && operation.IsNullable);
        Assert.Contains(up.OfType<DropCheckConstraintOperation>(), operation =>
            operation.Table == "matrix_details" && operation.Name == "ck_matrix_details_score");
        Assert.Contains(up.OfType<AddCheckConstraintOperation>(), operation =>
            operation.Table == "matrix_details" && operation.Name == "ck_matrix_details_score" &&
            operation.Sql == "allocated_score > 0");
        Assert.Contains(up.OfType<CreateIndexOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "idx_tasks_context");
        Assert.Contains(up.OfType<CreateIndexOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "idx_tasks_semester");
        Assert.Contains(up.OfType<AddForeignKeyOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "fk_tasks_context" &&
            operation.PrincipalTable == "academic_contexts");
        Assert.Contains(up.OfType<AddForeignKeyOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "fk_tasks_semester" &&
            operation.PrincipalTable == "semesters");

        Assert.Contains(down.OfType<DropColumnOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "academic_context_id");
        Assert.Contains(down.OfType<DropColumnOperation>(), operation =>
            operation.Table == "tasks" && operation.Name == "semester_id");
        Assert.Contains(down.OfType<AlterColumnOperation>(), operation =>
            operation.Table == "exam_matrices" && operation.Name == "task_id" && !operation.IsNullable);
        Assert.Contains(down.OfType<AddCheckConstraintOperation>(), operation =>
            operation.Table == "matrix_details" && operation.Name == "ck_matrix_details_score" &&
            operation.Sql == "allocated_score >= 0");
    }

    [Fact]
    public void DirectoryMigration_BackfillsEnrollmentsBeforeDroppingLegacyClassId()
    {
        var migration = new ExposedDirectoryMigration();
        var up = migration.BuildUp();

        Assert.Contains(up.OfType<CreateTableOperation>(), operation =>
            operation.Name == "student_class_enrollments");
        var backfill = up.ToList().FindIndex(operation =>
            operation is SqlOperation sql &&
            sql.Sql.Contains("INSERT INTO student_class_enrollments"));
        var drop = up.ToList().FindIndex(operation =>
            operation is DropColumnOperation column &&
            column.Table == "students" && column.Name == "class_id");
        Assert.True(backfill >= 0 && backfill < drop);
    }

    private sealed class ExposedDirectoryMigration : AddClassStudentDirectoryReadModel
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }
    }

    [Fact]
    public void EnrollmentHistoryMigration_CreatesReplacementIndexBeforeDroppingTheOldOnes()
    {
        var up = new ExposedEnrollmentHistoryMigration().BuildUp().ToList();

        // uq_student_enrollments_student_year is the only index backing
        // fk_student_enrollments_student until its replacement exists; dropping it first is errno 1553.
        var createNew = up.FindIndex(operation =>
            operation is CreateIndexOperation index &&
            index.Name == "uq_student_enrollments_student_active_year");
        var dropOld = up.FindIndex(operation =>
            operation is DropIndexOperation index &&
            index.Name == "uq_student_enrollments_student_year");
        Assert.True(createNew >= 0 && dropOld > createNew);

        var backfill = up.FindIndex(operation =>
            operation is SqlOperation sql && sql.Sql.Contains("SET e.started_on"));
        var startedOn = up.FindIndex(operation =>
            operation is AddColumnOperation column && column.Name == "started_on");
        Assert.True(startedOn >= 0 && backfill > startedOn);

        Assert.Contains(up.OfType<CreateTableOperation>(), operation =>
            operation.Name == "student_import_batches");
        Assert.Contains(up.OfType<CreateTableOperation>(), operation =>
            operation.Name == "student_import_rows");
    }

    private sealed class ExposedEnrollmentHistoryMigration : AddStudentImportAndEnrollmentHistory
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedInitialCreate : InitialCreate
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedIntegrityMigration : AddDatabaseIntegrityObjects
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDown()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Down(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedMatrixMigration : AddMatrixFeatureConstraints
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDown()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Down(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedPercentageScoringMigration : AddMatrixPercentageScoring
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDown()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Down(builder);
            return builder.Operations;
        }
    }

    private sealed class ExposedRemoveCodeMigration : RemoveMatrixCodeAddTaskName
    {
        public IReadOnlyList<MigrationOperation> BuildUp()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDown()
        {
            var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
            base.Down(builder);
            return builder.Operations;
        }
    }
}
