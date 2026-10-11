using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignExamManagementWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "end_time",
                table: "exams",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<ulong>(
                name: "grade_level_id",
                table: "exams",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "start_time",
                table: "exams",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<ulong>(
                name: "subject_id",
                table: "exams",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_exams_grade_level",
                table: "exams",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "idx_exams_subject",
                table: "exams",
                column: "subject_id");

            migrationBuilder.AddForeignKey(
                name: "fk_exams_grade_level",
                table: "exams",
                column: "grade_level_id",
                principalTable: "grade_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_exams_subject",
                table: "exams",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_exams_grade_level",
                table: "exams");

            migrationBuilder.DropForeignKey(
                name: "fk_exams_subject",
                table: "exams");

            migrationBuilder.DropIndex(
                name: "idx_exams_grade_level",
                table: "exams");

            migrationBuilder.DropIndex(
                name: "idx_exams_subject",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "end_time",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "grade_level_id",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "start_time",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "exams");
        }
    }
}
