using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassStudentDirectoryReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "admission_date",
                table: "students",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "students",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "students",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "students",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ACTIVE",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "classes",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE classes SET code = CONCAT('CLS-', id);");
            migrationBuilder.Sql(@"
UPDATE students s
LEFT JOIN users u ON u.id = s.user_id
JOIN classes c ON c.id = s.class_id
JOIN academic_years ay ON ay.id = c.academic_year_id
SET s.code = COALESCE(NULLIF(u.moet_identifier, ''), CONCAT('STU-', s.id)),
    s.full_name = COALESCE(NULLIF(u.full_name, ''), CONCAT('Học sinh ', s.id)),
    s.admission_date = ay.start_date,
    s.status = 'ACTIVE';");

            migrationBuilder.AddUniqueConstraint(
                name: "uq_classes_id_year",
                table: "classes",
                columns: new[] { "id", "academic_year_id" });

            migrationBuilder.CreateTable(
                name: "student_class_enrollments",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    student_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    school_class_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    academic_year_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_class_enrollments", x => x.id);
                    table.CheckConstraint("ck_student_enrollments_status", "status IN ('ACTIVE', 'COMPLETED', 'TRANSFERRED_OUT')");
                    table.ForeignKey(
                        name: "fk_student_enrollments_class_year",
                        columns: x => new { x.school_class_id, x.academic_year_id },
                        principalTable: "classes",
                        principalColumns: new[] { "id", "academic_year_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_enrollments_student",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_enrollments_year",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_students_status",
                table: "students",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_students_code",
                table: "students",
                column: "code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('ACTIVE', 'TEMPORARY_LEAVE', 'TRANSFERRED')");

            migrationBuilder.CreateIndex(
                name: "idx_classes_directory",
                table: "classes",
                columns: new[] { "school_branch_id", "academic_year_id", "grade_level_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_classes_branch_year_code",
                table: "classes",
                columns: new[] { "school_branch_id", "academic_year_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_student_enrollments_class_status",
                table: "student_class_enrollments",
                columns: new[] { "school_class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_student_class_enrollments_academic_year_id",
                table: "student_class_enrollments",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_class_enrollments_school_class_id_academic_year_id",
                table: "student_class_enrollments",
                columns: new[] { "school_class_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "uq_student_enrollments_student_year",
                table: "student_class_enrollments",
                columns: new[] { "student_id", "academic_year_id" },
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO student_class_enrollments (student_id, school_class_id, academic_year_id, status)
SELECT s.id, c.id, c.academic_year_id,
       CASE WHEN ay.status = 'ACTIVE' THEN 'ACTIVE' ELSE 'COMPLETED' END
FROM students s
JOIN classes c ON c.id = s.class_id
JOIN academic_years ay ON ay.id = c.academic_year_id;");

            // Guard: strict-mode division by zero aborts the migration if any student lost its enrollment.
            migrationBuilder.Sql(@"
UPDATE classes SET id = id
WHERE CASE WHEN (SELECT COUNT(*) FROM students) <> (SELECT COUNT(*) FROM student_class_enrollments)
  THEN 1 / 0 ELSE 0 END;");

            migrationBuilder.DropForeignKey(
                name: "fk_students_class",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_students_class",
                table: "students");

            migrationBuilder.DropColumn(
                name: "class_id",
                table: "students");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Technical rollback only: history collapses to each student's latest-year class.
            migrationBuilder.AddColumn<ulong>(
                name: "class_id",
                table: "students",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE students s
JOIN student_class_enrollments e ON e.id = (
    SELECT e2.id FROM student_class_enrollments e2
    JOIN academic_years ay ON ay.id = e2.academic_year_id
    WHERE e2.student_id = s.id
    ORDER BY ay.start_date DESC, e2.id DESC
    LIMIT 1)
SET s.class_id = e.school_class_id;");

            migrationBuilder.AlterColumn<ulong>(
                name: "class_id",
                table: "students",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_students_class",
                table: "students",
                column: "class_id");

            migrationBuilder.AddForeignKey(
                name: "fk_students_class",
                table: "students",
                column: "class_id",
                principalTable: "classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "student_class_enrollments");

            migrationBuilder.DropIndex(
                name: "idx_students_status",
                table: "students");

            migrationBuilder.DropIndex(
                name: "uq_students_code",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_classes_id_year",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "idx_classes_directory",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "uq_classes_branch_year_code",
                table: "classes");

            migrationBuilder.DropColumn(name: "admission_date", table: "students");
            migrationBuilder.DropColumn(name: "code", table: "students");
            migrationBuilder.DropColumn(name: "full_name", table: "students");
            migrationBuilder.DropColumn(name: "status", table: "students");
            migrationBuilder.DropColumn(name: "code", table: "classes");
        }
    }
}
