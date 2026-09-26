using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInactiveStudentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('ACTIVE', 'TEMPORARY_LEAVE', 'TRANSFERRED', 'INACTIVE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            // Rolling back drops the INACTIVE code, so logically deleted students go back to
            // TRANSFERRED instead of failing the constraint.
            migrationBuilder.Sql(
                "UPDATE students SET status = 'TRANSFERRED' WHERE status = 'INACTIVE';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('ACTIVE', 'TEMPORARY_LEAVE', 'TRANSFERRED')");
        }
    }
}
