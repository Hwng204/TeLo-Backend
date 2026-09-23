using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatrixPercentageScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_matrix_details_score",
                table: "matrix_details");

            migrationBuilder.RenameColumn(
                name: "allocated_score",
                table: "matrix_details",
                newName: "percentage");

            migrationBuilder.AddColumn<uint>(
                name: "total_score",
                table: "exam_matrices",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            // Backfill existing data: the old rule always meant a matrix's total was conceptually 10,
            // so every pre-existing row is assumed to have been out of 10. `percentage` at this point
            // still holds the old absolute score (the column was just renamed above). Clamped to
            // (0, 100] so any pre-existing out-of-range test/draft data cannot violate the new check
            // constraints added right after this.
            migrationBuilder.Sql("UPDATE exam_matrices SET total_score = 10 WHERE total_score = 0;");
            migrationBuilder.Sql(
                "UPDATE matrix_details SET percentage = LEAST(100, GREATEST(0.01, percentage / 10 * 100));");

            migrationBuilder.AddCheckConstraint(
                name: "ck_matrix_details_percentage",
                table: "matrix_details",
                sql: "percentage > 0 AND percentage <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_matrices_total_score",
                table: "exam_matrices",
                sql: "total_score > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_matrix_details_percentage",
                table: "matrix_details");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_matrices_total_score",
                table: "exam_matrices");

            // Convert back to an absolute score (using each matrix's own total_score) before that
            // column disappears, so a Down/Up round trip does not silently lose data.
            migrationBuilder.Sql(
                "UPDATE matrix_details d " +
                "JOIN exam_matrices m ON m.id = d.exam_matrix_id " +
                "SET d.percentage = GREATEST(0.01, ROUND(m.total_score * d.percentage / 100, 2));");

            migrationBuilder.DropColumn(
                name: "total_score",
                table: "exam_matrices");

            migrationBuilder.RenameColumn(
                name: "percentage",
                table: "matrix_details",
                newName: "allocated_score");

            migrationBuilder.AddCheckConstraint(
                name: "ck_matrix_details_score",
                table: "matrix_details",
                sql: "allocated_score > 0");
        }
    }
}
