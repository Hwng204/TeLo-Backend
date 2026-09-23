using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMatrixCodeAddTaskName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_exam_matrices_code",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "code",
                table: "exam_matrices");

            // Never an EF-mapped entity (created by raw SQL in AddMatrixAuthorshipAndCode), so
            // `migrations add` cannot see it on its own — dropped here by hand.
            migrationBuilder.Sql("DROP TABLE matrix_code_sequences;");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "tasks",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Best-effort backfill so existing matrix tasks are not nameless: reuse the work
            // description as the name. Non-matrix tasks (none exist yet) and matrix tasks with no
            // description are left NULL.
            migrationBuilder.Sql(
                "UPDATE tasks SET name = description " +
                "WHERE task_type = 'MATRIX' AND name IS NULL AND description IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "tasks");

            // Codes already assigned are gone for good (see Up); this only restores the column and
            // an empty sequence table so the app can start generating new codes again, matching how
            // the same non-symmetric trade-off was made when other data-removing migrations in this
            // project dropped columns/tables that Up cannot repopulate from nothing.
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "exam_matrices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // AddColumn above gives every existing row the same "" default; a unique index needs a
            // per-row placeholder first (the id makes it unique — never a real generated code).
            migrationBuilder.Sql("UPDATE exam_matrices SET code = CONCAT('MT-RESTORED-', id);");

            migrationBuilder.CreateIndex(
                name: "uq_exam_matrices_code",
                table: "exam_matrices",
                column: "code",
                unique: true);

            migrationBuilder.Sql(@"
                CREATE TABLE matrix_code_sequences (
                    year_number INT NOT NULL,
                    last_number BIGINT UNSIGNED NOT NULL,
                    PRIMARY KEY (year_number)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
        }
    }
}
