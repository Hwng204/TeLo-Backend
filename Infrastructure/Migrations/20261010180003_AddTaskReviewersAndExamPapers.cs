using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskReviewersAndExamPapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "exam_matrix_id",
                table: "tasks",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "reviewer_user_id",
                table: "tasks",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_task_papers",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_task_papers", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_task_papers_task",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_tasks_exam_matrix",
                table: "tasks",
                column: "exam_matrix_id");

            migrationBuilder.CreateIndex(
                name: "idx_tasks_reviewer",
                table: "tasks",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_question_tasks_task_lesson",
                table: "question_tasks",
                columns: new[] { "task_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_exam_task_papers_task",
                table: "exam_task_papers",
                column: "task_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_exam_matrix",
                table: "tasks",
                column: "exam_matrix_id",
                principalTable: "exam_matrices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_reviewer",
                table: "tasks",
                column: "reviewer_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // uq_question_tasks_task_lesson (task_id, lesson_id) now backs fk_question_tasks_task, so the old index can go.
            migrationBuilder.DropIndex(
                name: "uq_question_tasks_task",
                table: "question_tasks");

            // GB-53: "Đã nộp" gộp vào "Chờ kiểm duyệt". Nhiệm vụ ma trận đã có bản nháp là Đang thực hiện; bản nháp
            // bị từ chối mà chưa nộp lại (Submit xoá dấu từ chối) là Cần chỉnh sửa.
            migrationBuilder.Sql("UPDATE tasks SET status = 'PENDING_REVIEW' WHERE status = 'SUBMITTED';");
            migrationBuilder.Sql(
                """
                UPDATE tasks t JOIN exam_matrices m ON m.task_id = t.id
                SET t.status = CASE WHEN m.reject_comment IS NOT NULL OR m.rejected_at IS NOT NULL
                    THEN 'NEEDS_REVISION' ELSE 'IN_PROGRESS' END
                WHERE t.task_type = 'MATRIX' AND t.status = 'ASSIGNED' AND m.status = 'DRAFT';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL auto-commits: refuse multiple lessons before dropping any data or constraints.
            migrationBuilder.CreateIndex(
                name: "uq_question_tasks_task",
                table: "question_tasks",
                column: "task_id",
                unique: true);

            // Về lại bộ trạng thái cũ: Đã giao → Đã nộp → Hoàn thành.
            migrationBuilder.Sql("UPDATE tasks SET status = 'SUBMITTED' WHERE status = 'PENDING_REVIEW' AND task_type IN ('MATRIX', 'EXAM');");
            migrationBuilder.Sql("UPDATE tasks SET status = 'ASSIGNED' WHERE status IN ('IN_PROGRESS', 'NEEDS_REVISION') AND task_type = 'MATRIX';");

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_exam_matrix",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_reviewer",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "exam_task_papers");

            migrationBuilder.DropIndex(
                name: "idx_tasks_exam_matrix",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "idx_tasks_reviewer",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "uq_question_tasks_task_lesson",
                table: "question_tasks");

            migrationBuilder.DropColumn(
                name: "exam_matrix_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "reviewer_user_id",
                table: "tasks");
        }
    }
}
