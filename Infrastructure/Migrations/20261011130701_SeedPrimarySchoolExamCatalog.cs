using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedPrimarySchoolExamCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "grade_levels",
                columns: new[] { "id", "name", "status" },
                values: new object[,]
                {
                    { 1ul, "Khối 1", "ACTIVE" },
                    { 2ul, "Khối 2", "ACTIVE" },
                    { 3ul, "Khối 3", "ACTIVE" },
                    { 4ul, "Khối 4", "ACTIVE" },
                    { 5ul, "Khối 5", "ACTIVE" }
                });

            migrationBuilder.InsertData(
                table: "subjects",
                columns: new[] { "id", "name", "status" },
                values: new object[,]
                {
                    { 1ul, "Toán", "ACTIVE" },
                    { 2ul, "Tiếng Việt", "ACTIVE" },
                    { 3ul, "Tiếng Anh", "ACTIVE" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var id in new ulong[] { 1, 2, 3, 4, 5 })
            {
                migrationBuilder.DeleteData(
                    table: "grade_levels",
                    keyColumn: "id",
                    keyValue: id);
            }

            foreach (var id in new ulong[] { 1, 2, 3 })
            {
                migrationBuilder.DeleteData(
                    table: "subjects",
                    keyColumn: "id",
                    keyValue: id);
            }
        }
    }
}
