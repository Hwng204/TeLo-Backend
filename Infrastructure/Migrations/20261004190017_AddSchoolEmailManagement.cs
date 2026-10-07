using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolEmailManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "created_by_user_id",
                table: "notifications",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "email_template_version_id",
                table: "notifications",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event_code",
                table: "notifications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "event_key",
                table: "notifications",
                type: "varchar(190)",
                maxLength: 190,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_test",
                table: "notifications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "notification_recipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "delivery_token",
                table: "notification_recipients",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "email_address",
                table: "notification_recipients",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "notification_recipients",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_at",
                table: "notification_recipients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at",
                table: "notification_recipients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "email_template_version_id",
                table: "notification_configs",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "version",
                table: "notification_configs",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    version = table.Column<uint>(type: "int unsigned", nullable: false),
                    used_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.id);
                    table.CheckConstraint("ck_email_templates_status", "status IN ('ACTIVE','INACTIVE')");
                    table.ForeignKey(
                        name: "FK_email_templates_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "email_template_versions",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email_template_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    revision = table.Column<uint>(type: "int unsigned", nullable: false),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_template_versions_email_templates_email_template_id",
                        column: x => x.email_template_id,
                        principalTable: "email_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_email_template_version_id",
                table: "notifications",
                column: "email_template_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_event_key",
                table: "notifications",
                column: "event_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_recipients_email_status_next_attempt_at",
                table: "notification_recipients",
                columns: new[] { "email_status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_configs_email_template_version_id",
                table: "notification_configs",
                column: "email_template_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_template_versions_email_template_id_revision",
                table: "email_template_versions",
                columns: new[] { "email_template_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_school_id_code",
                table: "email_templates",
                columns: new[] { "school_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_school_id_event_code_status",
                table: "email_templates",
                columns: new[] { "school_id", "event_code", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_notification_configs_email_template_versions_email_template_~",
                table: "notification_configs",
                column: "email_template_version_id",
                principalTable: "email_template_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_email_template_versions_email_template_version~",
                table: "notifications",
                column: "email_template_version_id",
                principalTable: "email_template_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_configs_email_template_versions_email_template_~",
                table: "notification_configs");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_email_template_versions_email_template_version~",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "email_template_versions");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropIndex(
                name: "IX_notifications_email_template_version_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_event_key",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notification_recipients_email_status_next_attempt_at",
                table: "notification_recipients");

            migrationBuilder.DropIndex(
                name: "IX_notification_configs_email_template_version_id",
                table: "notification_configs");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "email_template_version_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "event_code",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "event_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "is_test",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "delivery_token",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "email_address",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "locked_at",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "email_template_version_id",
                table: "notification_configs");

            migrationBuilder.DropColumn(
                name: "version",
                table: "notification_configs");
        }
    }
}
