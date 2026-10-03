using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GlobalAcademicYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_academic_years_province",
                table: "academic_years");

            migrationBuilder.DropColumn(
                name: "representative",
                table: "schools");

            migrationBuilder.DropColumn(
                name: "type",
                table: "schools");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "academic_years");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "representative",
                table: "schools",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "schools",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "academic_years",
                type: "varchar(2)",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "fk_academic_years_province",
                table: "academic_years",
                column: "ProvinceCode",
                principalTable: "provinces",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
