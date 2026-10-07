using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailEventCatalogAndScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "notifications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_fingerprint",
                table: "notifications",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "send_kind",
                table: "notifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "AUTOMATIC",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<uint>(
                name: "version",
                table: "notifications",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.AddColumn<uint>(
                name: "event_version",
                table: "email_template_versions",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.AddColumn<string>(
                name: "variables_json",
                table: "email_template_versions",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "email_events",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    scope_key = table.Column<ulong>(type: "bigint unsigned", nullable: false, computedColumnSql: "COALESCE(school_id,0)", stored: true),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    trigger_kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    variables_json = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    version = table.Column<uint>(type: "int unsigned", nullable: false),
                    used_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_events", x => x.id);
                    table.CheckConstraint("ck_email_events_scope", "(trigger_kind='SYSTEM' AND school_id IS NULL) OR (trigger_kind='MANUAL' AND school_id IS NOT NULL)");
                    table.CheckConstraint("ck_email_events_status", "status IN ('ACTIVE','INACTIVE')");
                    table.ForeignKey(
                        name: "FK_email_events_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.InsertData(
                table: "email_events",
                columns: new[] { "id", "code", "description", "name", "school_id", "status", "trigger_kind", "used_at", "variables_json", "version" },
                values: new object[,]
                {
                    { 1ul, "MATRIX_ASSIGNED", "", "Giao nhiệm vụ lập ma trận", null, "ACTIVE", "SYSTEM", null, "[{\"Name\":\"schoolName\",\"Label\":\"schoolName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"branchName\",\"Label\":\"branchName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actorName\",\"Label\":\"actorName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"taskName\",\"Label\":\"taskName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"dueAt\",\"Label\":\"dueAt\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actionUrl\",\"Label\":\"actionUrl\",\"Type\":\"TEXT\",\"Required\":true}]", 1u },
                    { 2ul, "MATRIX_SUBMITTED", "", "Ma trận đã nộp", null, "ACTIVE", "SYSTEM", null, "[{\"Name\":\"schoolName\",\"Label\":\"schoolName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"branchName\",\"Label\":\"branchName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actorName\",\"Label\":\"actorName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"matrixName\",\"Label\":\"matrixName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actionUrl\",\"Label\":\"actionUrl\",\"Type\":\"TEXT\",\"Required\":true}]", 1u },
                    { 3ul, "MATRIX_APPROVED", "", "Ma trận đã duyệt / hoàn tất", null, "ACTIVE", "SYSTEM", null, "[{\"Name\":\"schoolName\",\"Label\":\"schoolName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"branchName\",\"Label\":\"branchName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actorName\",\"Label\":\"actorName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"matrixName\",\"Label\":\"matrixName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actionUrl\",\"Label\":\"actionUrl\",\"Type\":\"TEXT\",\"Required\":true}]", 1u },
                    { 4ul, "MATRIX_REJECTED", "", "Ma trận cần chỉnh sửa", null, "ACTIVE", "SYSTEM", null, "[{\"Name\":\"schoolName\",\"Label\":\"schoolName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"branchName\",\"Label\":\"branchName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actorName\",\"Label\":\"actorName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"matrixName\",\"Label\":\"matrixName\",\"Type\":\"TEXT\",\"Required\":true},{\"Name\":\"actionUrl\",\"Label\":\"actionUrl\",\"Type\":\"TEXT\",\"Required\":true}]", 1u }
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_events_school_id",
                table: "email_events",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_events_scope_key_code",
                table: "email_events",
                columns: new[] { "scope_key", "code" },
                unique: true);
            migrationBuilder.Sql("UPDATE email_template_versions v JOIN email_templates t ON t.id=v.email_template_id JOIN email_events e ON e.code=t.event_code AND e.school_id IS NULL SET v.variables_json=e.variables_json WHERE v.variables_json IS NULL;");
            migrationBuilder.Sql("UPDATE notifications SET send_kind='TEST' WHERE is_test=1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_events");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "request_fingerprint",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "send_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "version",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "event_version",
                table: "email_template_versions");

            migrationBuilder.DropColumn(
                name: "variables_json",
                table: "email_template_versions");
        }
    }
}
