using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatrixAuthorshipAndCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "exam_matrices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "approved_by_user_id",
                table: "exam_matrices",
                type: "bigint unsigned",
                nullable: true);

            // code and created_at are added as NULLable, filled for the rows that already exist,
            // and only then tightened to NOT NULL (a default would make every old code identical
            // and break the unique index).
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "exam_matrices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "exam_matrices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "created_by_user_id",
                table: "exam_matrices",
                type: "bigint unsigned",
                nullable: true);

            // Existing matrices: a task-bound matrix was written by the Team Lead the task was
            // assigned to, and its creation time is the task's. A direct matrix has no recorded
            // author, so created_by stays NULL and created_at falls back to the migration time
            // (an approximation: the real date was never stored).
            migrationBuilder.Sql(@"
                UPDATE exam_matrices m
                LEFT JOIN tasks t ON t.id = m.task_id
                SET m.created_by_user_id = t.assigned_to_user_id,
                    m.created_at = COALESCE(t.created_at, UTC_TIMESTAMP(6));");

            // MT-{year}-{running number per year, in id order}. The year follows Vietnam time (UTC+7).
            migrationBuilder.Sql(@"
                UPDATE exam_matrices m
                JOIN (
                    SELECT id,
                           YEAR(DATE_ADD(created_at, INTERVAL 7 HOUR)) AS yr,
                           ROW_NUMBER() OVER (
                               PARTITION BY YEAR(DATE_ADD(created_at, INTERVAL 7 HOUR))
                               ORDER BY id) AS seq
                    FROM exam_matrices
                ) numbered ON numbered.id = m.id
                SET m.code = CONCAT('MT-', numbered.yr, '-', LPAD(numbered.seq, 3, '0'));");

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "exam_matrices",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_0900_ai_ci",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "exam_matrices",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            // Per-year counter behind the code. Bumped atomically with LAST_INSERT_ID(expr) inside the
            // creating transaction, so concurrent creates never get the same number and a rolled-back
            // create gives its number back. Not an EF entity: only the repository touches it, in raw SQL.
            migrationBuilder.Sql(@"
                CREATE TABLE matrix_code_sequences (
                    year_number INT NOT NULL,
                    last_number BIGINT UNSIGNED NOT NULL,
                    PRIMARY KEY (year_number)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            migrationBuilder.Sql(@"
                INSERT INTO matrix_code_sequences (year_number, last_number)
                SELECT YEAR(DATE_ADD(created_at, INTERVAL 7 HOUR)), COUNT(*)
                FROM exam_matrices
                GROUP BY YEAR(DATE_ADD(created_at, INTERVAL 7 HOUR));");

            migrationBuilder.CreateIndex(
                name: "idx_exam_matrices_approver",
                table: "exam_matrices",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_exam_matrices_creator",
                table: "exam_matrices",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_matrices_code",
                table: "exam_matrices",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_exam_matrices_approver",
                table: "exam_matrices",
                column: "approved_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_exam_matrices_creator",
                table: "exam_matrices",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE matrix_code_sequences;");

            migrationBuilder.DropForeignKey(
                name: "fk_exam_matrices_approver",
                table: "exam_matrices");

            migrationBuilder.DropForeignKey(
                name: "fk_exam_matrices_creator",
                table: "exam_matrices");

            migrationBuilder.DropIndex(
                name: "idx_exam_matrices_approver",
                table: "exam_matrices");

            migrationBuilder.DropIndex(
                name: "idx_exam_matrices_creator",
                table: "exam_matrices");

            migrationBuilder.DropIndex(
                name: "uq_exam_matrices_code",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "code",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "exam_matrices");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "exam_matrices");
        }
    }
}
