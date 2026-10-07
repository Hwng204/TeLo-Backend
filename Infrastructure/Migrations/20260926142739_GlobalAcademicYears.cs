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
            // AcademicYearSystemScope (20260926120000) already performs the
            // complete province-to-system migration earlier in this chain.
            // Keep this historical migration identifier, but do not repeat its
            // FK removal or drop the unrelated legacy schools.type/representative
            // columns introduced by a divergent scaffold. This is intentionally
            // a no-op for both fresh databases and previously migrated databases.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Up owns no schema changes. AcademicYearSystemScope.Down restores
            // the province schema if rollback continues past that migration.
        }
    }
}
