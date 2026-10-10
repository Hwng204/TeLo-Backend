using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudentActiveCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_students_code",
                table: "students");

            migrationBuilder.DropIndex(
                name: "uq_students_active_code",
                table: "students");

            migrationBuilder.DropColumn(
                name: "active_code",
                table: "students");

            migrationBuilder.CreateIndex(
                name: "uq_students_code",
                table: "students",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_students_code",
                table: "students");

            migrationBuilder.AddColumn<string>(
                name: "active_code",
                table: "students",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "CASE WHEN status <> 'INACTIVE' THEN code ELSE NULL END",
                stored: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_students_code",
                table: "students",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "uq_students_active_code",
                table: "students",
                column: "active_code",
                unique: true);
        }
    }
}
