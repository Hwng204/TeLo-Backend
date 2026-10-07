using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class GlobalAcademicYears : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AcademicYearSystemScope already removed the province relationship.
            // Keep this historical migration without repeating schema changes.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: Up does not change the schema.
        }
    }
}