using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionPaperLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "exam_set_id",
                table: "exam_task_papers",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_task_papers_exam_set_id",
                table: "exam_task_papers",
                column: "exam_set_id");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_task_papers_exam_sets_exam_set_id",
                table: "exam_task_papers",
                column: "exam_set_id",
                principalTable: "exam_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_task_papers_exam_sets_exam_set_id",
                table: "exam_task_papers");

            migrationBuilder.DropIndex(
                name: "IX_exam_task_papers_exam_set_id",
                table: "exam_task_papers");

            migrationBuilder.DropColumn(
                name: "exam_set_id",
                table: "exam_task_papers");
        }
    }
}
