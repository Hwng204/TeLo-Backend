using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "academic_year",
                columns: table => new
                {
                    academic_year_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "DRAFT", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_year", x => x.academic_year_id);
                    table.CheckConstraint("ck_academic_year_status", "status IN ('DRAFT', 'ACTIVE', 'CLOSED')");
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "grade_level",
                columns: table => new
                {
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grade_level", x => x.grade_level_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "incident_type",
                columns: table => new
                {
                    incident_type_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_type", x => x.incident_type_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "module",
                columns: table => new
                {
                    module_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    image = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module", x => x.module_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "notification_config",
                columns: table => new
                {
                    notification_config_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subject_template = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title_template = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_template = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_config", x => x.notification_config_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request_process",
                columns: table => new
                {
                    request_process_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_process", x => x.request_process_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    role_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.role_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "school",
                columns: table => new
                {
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    province = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school", x => x.school_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "subject",
                columns: table => new
                {
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject", x => x.subject_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "violation_type",
                columns: table => new
                {
                    violation_type_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violation_type", x => x.violation_type_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "semester",
                columns: table => new
                {
                    semester_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    academic_year_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semester", x => x.semester_id);
                    table.CheckConstraint("ck_semester_code", "code IN ('1','2')");
                    table.ForeignKey(
                        name: "fk_semester_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_year",
                        principalColumn: "academic_year_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "navbar",
                columns: table => new
                {
                    navbar_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    module_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    parent_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    display_order = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    url_path = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    icon = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_navbar", x => x.navbar_id);
                    table.ForeignKey(
                        name: "fk_navbar_module_id",
                        column: x => x.module_id,
                        principalTable: "module",
                        principalColumn: "module_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_navbar_parent_id",
                        column: x => x.parent_id,
                        principalTable: "navbar",
                        principalColumn: "navbar_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    notification_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    notification_config_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    subject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.notification_id);
                    table.ForeignKey(
                        name: "fk_notification_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_config",
                        principalColumn: "notification_config_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "parameter",
                columns: table => new
                {
                    parameter_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    notification_config_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parameter", x => x.parameter_id);
                    table.ForeignKey(
                        name: "fk_parameter_notification_config_id",
                        column: x => x.notification_config_id,
                        principalTable: "notification_config",
                        principalColumn: "notification_config_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request_step",
                columns: table => new
                {
                    request_step_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_process_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    step_order = table.Column<uint>(type: "int unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    handler_role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_step", x => x.request_step_id);
                    table.ForeignKey(
                        name: "fk_request_step_request_process_id",
                        column: x => x.request_process_id,
                        principalTable: "request_process",
                        principalColumn: "request_process_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "branch",
                columns: table => new
                {
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    logo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch", x => x.branch_id);
                    table.ForeignKey(
                        name: "fk_branch_school_id",
                        column: x => x.school_id,
                        principalTable: "school",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "permission",
                columns: table => new
                {
                    role_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    navbar_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    permission = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission", x => new { x.role_id, x.navbar_id, x.school_id, x.permission });
                    table.ForeignKey(
                        name: "fk_permission_navbar_id",
                        column: x => x.navbar_id,
                        principalTable: "navbar",
                        principalColumn: "navbar_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_school_id",
                        column: x => x.school_id,
                        principalTable: "school",
                        principalColumn: "school_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "chapter",
                columns: table => new
                {
                    chapter_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter", x => x.chapter_id);
                    table.ForeignKey(
                        name: "fk_chapter_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chapter_grade_level_id",
                        column: x => x.grade_level_id,
                        principalTable: "grade_level",
                        principalColumn: "grade_level_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chapter_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "class",
                columns: table => new
                {
                    class_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    academic_year_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class", x => x.class_id);
                    table.ForeignKey(
                        name: "fk_class_academic_year_id",
                        column: x => x.academic_year_id,
                        principalTable: "academic_year",
                        principalColumn: "academic_year_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_grade_level_id",
                        column: x => x.grade_level_id,
                        principalTable: "grade_level",
                        principalColumn: "grade_level_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    username = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    password = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    full_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    moet_identifier = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    avatar_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    security_version = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "lesson",
                columns: table => new
                {
                    lesson_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    chapter_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson", x => x.lesson_id);
                    table.ForeignKey(
                        name: "fk_lesson_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "chapter",
                        principalColumn: "chapter_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "notification_user",
                columns: table => new
                {
                    notification_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_user", x => new { x.notification_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_notification_user_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notification",
                        principalColumn: "notification_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notification_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request",
                columns: table => new
                {
                    request_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_process_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    note = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    priority = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request", x => x.request_id);
                    table.ForeignKey(
                        name: "fk_request_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_created_by",
                        column: x => x.created_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_request_process_id",
                        column: x => x.request_process_id,
                        principalTable: "request_process",
                        principalColumn: "request_process_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "student",
                columns: table => new
                {
                    student_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student", x => x.student_id);
                    table.ForeignKey(
                        name: "fk_student_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "task",
                columns: table => new
                {
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    assigned_to_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    due_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    task_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    task_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task", x => x.task_id);
                    table.ForeignKey(
                        name: "fk_task_assigned_to_user_id",
                        column: x => x.assigned_to_user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "teacher",
                columns: table => new
                {
                    teacher_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    specialization = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    gender = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher", x => x.teacher_id);
                    table.ForeignKey(
                        name: "fk_teacher_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    role_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_roles_role",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_user",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request_attachment",
                columns: table => new
                {
                    request_attachment_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    request_step_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    path = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    uploaded_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_attachment", x => x.request_attachment_id);
                    table.ForeignKey(
                        name: "fk_request_attachment_request_id",
                        column: x => x.request_id,
                        principalTable: "request",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_attachment_request_step_id",
                        column: x => x.request_step_id,
                        principalTable: "request_step",
                        principalColumn: "request_step_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_attachment_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request_comment",
                columns: table => new
                {
                    request_comment_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    create_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_comment", x => x.request_comment_id);
                    table.ForeignKey(
                        name: "fk_request_comment_create_by",
                        column: x => x.create_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_comment_request_id",
                        column: x => x.request_id,
                        principalTable: "request",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "request_history",
                columns: table => new
                {
                    request_history_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    request_step_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    handle_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_history", x => x.request_history_id);
                    table.ForeignKey(
                        name: "fk_request_history_handle_by",
                        column: x => x.handle_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_history_request_id",
                        column: x => x.request_id,
                        principalTable: "request",
                        principalColumn: "request_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_history_request_step_id",
                        column: x => x.request_step_id,
                        principalTable: "request_step",
                        principalColumn: "request_step_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "student_enrollment",
                columns: table => new
                {
                    student_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    class_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    enroll_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    left_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_enrollment", x => new { x.class_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_student_enrollment_class_id",
                        column: x => x.class_id,
                        principalTable: "class",
                        principalColumn: "class_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_enrollment_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "matrix",
                columns: table => new
                {
                    matrix_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    create_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    total_score = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrix", x => x.matrix_id);
                    table.CheckConstraint("ck_matrix_total_score", "total_score > 0");
                    table.ForeignKey(
                        name: "fk_matrix_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matrix_create_by",
                        column: x => x.create_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matrix_grade_level_id",
                        column: x => x.grade_level_id,
                        principalTable: "grade_level",
                        principalColumn: "grade_level_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matrix_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matrix_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "question_task",
                columns: table => new
                {
                    question_task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    lesson_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_question = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_task", x => x.question_task_id);
                    table.CheckConstraint("ck_question_task_count", "total_question > 0");
                    table.ForeignKey(
                        name: "fk_question_task_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lesson",
                        principalColumn: "lesson_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_question_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "task_history",
                columns: table => new
                {
                    task_history_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    create_by = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    create_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_history", x => x.task_history_id);
                    table.ForeignKey(
                        name: "fk_task_history_create_by",
                        column: x => x.create_by,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_history_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "teacher_assignment",
                columns: table => new
                {
                    class_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    teacher_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    assignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_assignment", x => new { x.class_id, x.teacher_id, x.subject_id });
                    table.ForeignKey(
                        name: "fk_teacher_assignment_class_id",
                        column: x => x.class_id,
                        principalTable: "class",
                        principalColumn: "class_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_assignment_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_assignment_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teacher",
                        principalColumn: "teacher_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam",
                columns: table => new
                {
                    exam_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    semester_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    matrix_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    result_published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam", x => x.exam_id);
                    table.ForeignKey(
                        name: "fk_exam_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_grade_level_id",
                        column: x => x.grade_level_id,
                        principalTable: "grade_level",
                        principalColumn: "grade_level_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_matrix_id",
                        column: x => x.matrix_id,
                        principalTable: "matrix",
                        principalColumn: "matrix_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semester",
                        principalColumn: "semester_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "subject_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam_set",
                columns: table => new
                {
                    exam_set_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    matrix_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    task_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_minutes = table.Column<uint>(type: "int unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    review_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_set", x => x.exam_set_id);
                    table.ForeignKey(
                        name: "fk_exam_set_matrix_id",
                        column: x => x.matrix_id,
                        principalTable: "matrix",
                        principalColumn: "matrix_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_set_review_by_user_id",
                        column: x => x.review_by_user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_exam_set_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "matrix_detail",
                columns: table => new
                {
                    matrix_detail_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    matrix_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    lesson_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    cognitive_level = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    question_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    question_count = table.Column<uint>(type: "int unsigned", nullable: false),
                    allocated_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matrix_detail", x => x.matrix_detail_id);
                    table.CheckConstraint("ck_matrix_detail_allocated_score", "allocated_score > 0 AND allocated_score <= 100");
                    table.CheckConstraint("ck_matrix_detail_count", "question_count > 0");
                    table.ForeignKey(
                        name: "fk_matrix_detail_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lesson",
                        principalColumn: "lesson_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matrix_detail_matrix_id",
                        column: x => x.matrix_id,
                        principalTable: "matrix",
                        principalColumn: "matrix_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "question_task_detail",
                columns: table => new
                {
                    question_task_detail_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    question_task_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    cognitive_level = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    question_count = table.Column<uint>(type: "int unsigned", nullable: false),
                    question_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_task_detail", x => x.question_task_detail_id);
                    table.CheckConstraint("ck_question_task_detail_count", "question_count > 0");
                    table.ForeignKey(
                        name: "fk_question_task_detail_question_task_id",
                        column: x => x.question_task_id,
                        principalTable: "question_task",
                        principalColumn: "question_task_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam_session",
                columns: table => new
                {
                    exam_session_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    exam_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_session", x => x.exam_session_id);
                    table.ForeignKey(
                        name: "fk_exam_session_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exam",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam_subject",
                columns: table => new
                {
                    exam_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    exam_set_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    is_primary_exam = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_subject", x => new { x.exam_id, x.exam_set_id });
                    table.ForeignKey(
                        name: "fk_exam_subject_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exam",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_subject_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_set",
                        principalColumn: "exam_set_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam_variant",
                columns: table => new
                {
                    exam_variant_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    exam_set_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    variant_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    question_order = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_variant", x => x.exam_variant_id);
                    table.ForeignKey(
                        name: "fk_exam_variant_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_set",
                        principalColumn: "exam_set_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "question",
                columns: table => new
                {
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    question_task_details_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    lesson_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    bank_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    answer_explanation = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question", x => x.question_id);
                    table.ForeignKey(
                        name: "fk_question_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_question_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lesson",
                        principalColumn: "lesson_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_question_question_task_details_id",
                        column: x => x.question_task_details_id,
                        principalTable: "question_task_detail",
                        principalColumn: "question_task_detail_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "room",
                columns: table => new
                {
                    room_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    exam_session_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    capacity = table.Column<uint>(type: "int unsigned", nullable: false),
                    access_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room", x => x.room_id);
                    table.ForeignKey(
                        name: "fk_room_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_session",
                        principalColumn: "exam_session_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "test_result",
                columns: table => new
                {
                    test_result_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    exam_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    exam_variant_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    total_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    correct_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    wrong_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_result", x => x.test_result_id);
                    table.ForeignKey(
                        name: "fk_test_result_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exam",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_test_result_exam_variant_id",
                        column: x => x.exam_variant_id,
                        principalTable: "exam_variant",
                        principalColumn: "exam_variant_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_test_result_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "answer",
                columns: table => new
                {
                    answer_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_correct = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    option_key = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answer", x => x.answer_id);
                    table.ForeignKey(
                        name: "fk_answer_question_id",
                        column: x => x.question_id,
                        principalTable: "question",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "exam_set_question",
                columns: table => new
                {
                    exam_set_question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    exam_set_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_set_question", x => x.exam_set_question_id);
                    table.ForeignKey(
                        name: "fk_exam_set_question_exam_set_id",
                        column: x => x.exam_set_id,
                        principalTable: "exam_set",
                        principalColumn: "exam_set_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_set_question_question_id",
                        column: x => x.question_id,
                        principalTable: "question",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "invigilator",
                columns: table => new
                {
                    teacher_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    room_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    position = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invigilator", x => new { x.teacher_id, x.room_id });
                    table.ForeignKey(
                        name: "fk_invigilator_room_id",
                        column: x => x.room_id,
                        principalTable: "room",
                        principalColumn: "room_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_invigilator_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teacher",
                        principalColumn: "teacher_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "room_student",
                columns: table => new
                {
                    room_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_student", x => new { x.room_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_room_student_room_id",
                        column: x => x.room_id,
                        principalTable: "room",
                        principalColumn: "room_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_room_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "incident",
                columns: table => new
                {
                    incident_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    test_result_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    incident_type_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident", x => x.incident_id);
                    table.ForeignKey(
                        name: "fk_incident_incident_type_id",
                        column: x => x.incident_type_id,
                        principalTable: "incident_type",
                        principalColumn: "incident_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incident_test_result_id",
                        column: x => x.test_result_id,
                        principalTable: "test_result",
                        principalColumn: "test_result_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "test_result_detail",
                columns: table => new
                {
                    test_result_detail_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    test_result_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    selected_option_key = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score_awarded = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_result_detail", x => x.test_result_detail_id);
                    table.ForeignKey(
                        name: "fk_test_result_detail_question_id",
                        column: x => x.question_id,
                        principalTable: "question",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_test_result_detail_test_result_id",
                        column: x => x.test_result_id,
                        principalTable: "test_result",
                        principalColumn: "test_result_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "violation",
                columns: table => new
                {
                    violation_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    test_result_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    violation_type_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    decision = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violation", x => x.violation_id);
                    table.ForeignKey(
                        name: "fk_violation_test_result_id",
                        column: x => x.test_result_id,
                        principalTable: "test_result",
                        principalColumn: "test_result_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_violation_violation_type_id",
                        column: x => x.violation_type_id,
                        principalTable: "violation_type",
                        principalColumn: "violation_type_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "uq_academic_year_name",
                table: "academic_year",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_answer_question_id_option_key",
                table: "answer",
                columns: new[] { "question_id", "option_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_branch_school_id_code",
                table: "branch",
                columns: new[] { "school_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_chapters_grade",
                table: "chapter",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_subject_id",
                table: "chapter",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "uq_chapter_branch_id_subject_id_grade_level_id_title",
                table: "chapter",
                columns: new[] { "branch_id", "subject_id", "grade_level_id", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_classes_directory",
                table: "class",
                columns: new[] { "branch_id", "academic_year_id", "grade_level_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_classes_grade",
                table: "class",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_academic_year_id",
                table: "class",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "uq_class_branch_id_academic_year_id_name",
                table: "class",
                columns: new[] { "branch_id", "academic_year_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_branch_id",
                table: "exam",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_grade_level_id",
                table: "exam",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_matrix_id",
                table: "exam",
                column: "matrix_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_semester_id",
                table: "exam",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_subject_id",
                table: "exam",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_session_exam_id_code",
                table: "exam_session",
                columns: new[] { "exam_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_matrix_id",
                table: "exam_set",
                column: "matrix_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_review_by_user_id",
                table: "exam_set",
                column: "review_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_task_id",
                table: "exam_set",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_set_question_question_id",
                table: "exam_set_question",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "uq_exam_set_question_exam_set_id_question_id",
                table: "exam_set_question",
                columns: new[] { "exam_set_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exam_subject_exam_set_id",
                table: "exam_subject",
                column: "exam_set_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_exam_variant_exam_set_id_variant_code",
                table: "exam_variant",
                columns: new[] { "exam_set_id", "variant_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_grade_level_name",
                table: "grade_level",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incident_incident_type_id",
                table: "incident",
                column: "incident_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_test_result_id",
                table: "incident",
                column: "test_result_id");

            migrationBuilder.CreateIndex(
                name: "uq_incident_type_name",
                table: "incident_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invigilator_room_id",
                table: "invigilator",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "uq_lesson_chapter_id_title",
                table: "lesson",
                columns: new[] { "chapter_id", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matrix_branch_id",
                table: "matrix",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_matrix_create_by",
                table: "matrix",
                column: "create_by");

            migrationBuilder.CreateIndex(
                name: "IX_matrix_grade_level_id",
                table: "matrix",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_matrix_subject_id",
                table: "matrix",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "uq_matrix_task_id",
                table: "matrix",
                column: "task_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matrix_detail_lesson_id",
                table: "matrix_detail",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "uq_matrix_detail_matrix_id_lesson_id_cog_level_question_type",
                table: "matrix_detail",
                columns: new[] { "matrix_id", "lesson_id", "cognitive_level", "question_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_module_name",
                table: "module",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_navbars_module_parent_order",
                table: "navbar",
                columns: new[] { "module_id", "parent_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_navbar_parent_id",
                table: "navbar",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_navbar_code",
                table: "navbar",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_notification_config_id",
                table: "notification",
                column: "notification_config_id");

            migrationBuilder.CreateIndex(
                name: "uq_notification_config_event_code",
                table: "notification_config",
                column: "event_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_user_user_id",
                table: "notification_user",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_parameter_notification_config_id_code",
                table: "parameter",
                columns: new[] { "notification_config_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_permissions_navbar",
                table: "permission",
                column: "navbar_id");

            migrationBuilder.CreateIndex(
                name: "IX_permission_school_id",
                table: "permission",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "idx_questions_creator",
                table: "question",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_questions_task_detail",
                table: "question",
                column: "question_task_details_id");

            migrationBuilder.CreateIndex(
                name: "IX_question_lesson_id",
                table: "question",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "idx_question_tasks_lesson",
                table: "question_task",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "uq_question_task_task_id_lesson_id",
                table: "question_task",
                columns: new[] { "task_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_question_task_detail_question_task_id_cog_level_question_type",
                table: "question_task_detail",
                columns: new[] { "question_task_id", "cognitive_level", "question_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_request_branch_id",
                table: "request",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_created_by",
                table: "request",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_request_request_process_id",
                table: "request",
                column: "request_process_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_attachment_request_id",
                table: "request_attachment",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_attachment_request_step_id",
                table: "request_attachment",
                column: "request_step_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_attachment_uploaded_by",
                table: "request_attachment",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_request_comment_create_by",
                table: "request_comment",
                column: "create_by");

            migrationBuilder.CreateIndex(
                name: "IX_request_comment_request_id",
                table: "request_comment",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_history_handle_by",
                table: "request_history",
                column: "handle_by");

            migrationBuilder.CreateIndex(
                name: "IX_request_history_request_id",
                table: "request_history",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_history_request_step_id",
                table: "request_history",
                column: "request_step_id");

            migrationBuilder.CreateIndex(
                name: "uq_request_process_name",
                table: "request_process",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_request_step_request_process_id_step_order",
                table: "request_step",
                columns: new[] { "request_process_id", "step_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_role_code",
                table: "role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_room_exam_session_id_code",
                table: "room",
                columns: new[] { "exam_session_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_student_student_id",
                table: "room_student",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_school_code",
                table: "school",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_semester_academic_year_id_code",
                table: "semester",
                columns: new[] { "academic_year_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_student_user_id",
                table: "student",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollment_student_id",
                table: "student_enrollment",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_subject_code",
                table: "subject",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tasks_assignee_status_due",
                table: "task",
                columns: new[] { "assigned_to_user_id", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "idx_tasks_creator",
                table: "task",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_history_create_by",
                table: "task_history",
                column: "create_by");

            migrationBuilder.CreateIndex(
                name: "IX_task_history_task_id",
                table: "task_history",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "uq_teacher_user_id",
                table: "teacher",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_assignment_subject_id",
                table: "teacher_assignment",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_assignment_teacher_id",
                table: "teacher_assignment",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_result_exam_id",
                table: "test_result",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_result_exam_variant_id",
                table: "test_result",
                column: "exam_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_result_student_id",
                table: "test_result",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_result_detail_question_id",
                table: "test_result_detail",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "uq_test_result_detail_test_result_id_question_id",
                table: "test_result_detail",
                columns: new[] { "test_result_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_branch_status",
                table: "user",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_user_moet_identifier",
                table: "user",
                column: "moet_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_username",
                table: "user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_role_user",
                table: "user_roles",
                columns: new[] { "role_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "uq_user_roles_user_role",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_violation_test_result_id",
                table: "violation",
                column: "test_result_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_violation_type_id",
                table: "violation",
                column: "violation_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_violation_type_name",
                table: "violation_type",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer");

            migrationBuilder.DropTable(
                name: "exam_set_question");

            migrationBuilder.DropTable(
                name: "exam_subject");

            migrationBuilder.DropTable(
                name: "incident");

            migrationBuilder.DropTable(
                name: "invigilator");

            migrationBuilder.DropTable(
                name: "matrix_detail");

            migrationBuilder.DropTable(
                name: "notification_user");

            migrationBuilder.DropTable(
                name: "parameter");

            migrationBuilder.DropTable(
                name: "permission");

            migrationBuilder.DropTable(
                name: "request_attachment");

            migrationBuilder.DropTable(
                name: "request_comment");

            migrationBuilder.DropTable(
                name: "request_history");

            migrationBuilder.DropTable(
                name: "room_student");

            migrationBuilder.DropTable(
                name: "student_enrollment");

            migrationBuilder.DropTable(
                name: "task_history");

            migrationBuilder.DropTable(
                name: "teacher_assignment");

            migrationBuilder.DropTable(
                name: "test_result_detail");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "violation");

            migrationBuilder.DropTable(
                name: "incident_type");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "navbar");

            migrationBuilder.DropTable(
                name: "request");

            migrationBuilder.DropTable(
                name: "request_step");

            migrationBuilder.DropTable(
                name: "room");

            migrationBuilder.DropTable(
                name: "class");

            migrationBuilder.DropTable(
                name: "teacher");

            migrationBuilder.DropTable(
                name: "question");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "test_result");

            migrationBuilder.DropTable(
                name: "violation_type");

            migrationBuilder.DropTable(
                name: "notification_config");

            migrationBuilder.DropTable(
                name: "module");

            migrationBuilder.DropTable(
                name: "request_process");

            migrationBuilder.DropTable(
                name: "exam_session");

            migrationBuilder.DropTable(
                name: "question_task_detail");

            migrationBuilder.DropTable(
                name: "exam_variant");

            migrationBuilder.DropTable(
                name: "student");

            migrationBuilder.DropTable(
                name: "exam");

            migrationBuilder.DropTable(
                name: "question_task");

            migrationBuilder.DropTable(
                name: "exam_set");

            migrationBuilder.DropTable(
                name: "semester");

            migrationBuilder.DropTable(
                name: "lesson");

            migrationBuilder.DropTable(
                name: "matrix");

            migrationBuilder.DropTable(
                name: "academic_year");

            migrationBuilder.DropTable(
                name: "chapter");

            migrationBuilder.DropTable(
                name: "task");

            migrationBuilder.DropTable(
                name: "grade_level");

            migrationBuilder.DropTable(
                name: "subject");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "branch");

            migrationBuilder.DropTable(
                name: "school");
        }
    }
}
