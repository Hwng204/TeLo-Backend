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
            // AcademicYearSystemScope already removed the province relationship.
            // The school columns in the original generated migration never existed
            // in the canonical migration chain, so this migration only keeps the
            // historical model checkpoint.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: Up does not change the schema.
        }
    }
}
