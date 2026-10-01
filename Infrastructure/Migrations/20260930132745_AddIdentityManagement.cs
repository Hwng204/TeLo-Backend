using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "roles",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                table: "roles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<ulong>(
                name: "school_branch_id",
                table: "roles",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "school_id",
                table: "roles",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "roles",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ACTIVE",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "used_at",
                table: "roles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "version",
                table: "roles",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "modules",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<uint>(
                name: "version",
                table: "modules",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.CreateTable(
                name: "identity_audits",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    actor_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    entity_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    action = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_audits", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_roles_school_branch_id",
                table: "roles",
                column: "school_branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_school_id",
                table: "roles",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_status_school_id_school_branch_id",
                table: "roles",
                columns: new[] { "status", "school_id", "school_branch_id" });

            // Backfill before creating the unique index. Existing module names/links
            // and all role grants remain intact; no unrelated legacy columns are dropped.
            migrationBuilder.Sql("UPDATE modules SET code = CONCAT('MODULE_', id);");
            migrationBuilder.Sql("""
                UPDATE roles SET is_system = 1
                WHERE code IN ('ADMIN', 'OperationalAdmin', 'TEACHER', 'GIAO_VIEN', 'STUDENT',
                    'HIEU_TRUONG', 'PRINCIPAL', 'PHT', 'TEAM_LEAD', 'TO_TRUONG');
                UPDATE roles r SET used_at = UTC_TIMESTAMP(6)
                WHERE EXISTS (SELECT 1 FROM user_roles ur WHERE ur.role_id = r.id)
                   OR EXISTS (SELECT 1 FROM permissions p WHERE p.role_id = r.id);
                INSERT INTO identity_audits(actor_user_id, entity_type, entity_id, action, data, created_at)
                SELECT 0, 'ROLE', r.id, 'BASELINE_GRANT',
                    JSON_OBJECT('UserId', ur.user_id, 'Code', r.code, 'Note', 'Existing grant; original grant date unknown'), UTC_TIMESTAMP(6)
                FROM user_roles ur JOIN roles r ON r.id = ur.role_id;
                """);

            migrationBuilder.CreateIndex(
                name: "uq_modules_code",
                table: "modules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_audits_entity_type_entity_id_created_at",
                table: "identity_audits",
                columns: new[] { "entity_type", "entity_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_roles_school_branches_school_branch_id",
                table: "roles",
                column: "school_branch_id",
                principalTable: "school_branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_roles_schools_school_id",
                table: "roles",
                column: "school_id",
                principalTable: "schools",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roles_school_branches_school_branch_id",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "FK_roles_schools_school_id",
                table: "roles");

            migrationBuilder.DropTable(
                name: "identity_audits");

            migrationBuilder.DropIndex(
                name: "IX_roles_school_branch_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_school_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_status_school_id_school_branch_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "uq_modules_code",
                table: "modules");

            migrationBuilder.DropColumn(
                name: "description",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_system",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "school_branch_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "school_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "status",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "used_at",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "version",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "code",
                table: "modules");

            migrationBuilder.DropColumn(
                name: "version",
                table: "modules");

        }
    }
}
