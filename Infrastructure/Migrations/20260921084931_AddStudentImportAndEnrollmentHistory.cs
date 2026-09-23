using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentImportAndEnrollmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters: the old unique indexes are dropped at the very end of Up. Until the
            // replacement (student_id, active_year_key) index exists, uq_student_enrollments_student_year
            // is the only index InnoDB can use to back fk_student_enrollments_student (errno 1553).
            migrationBuilder.AddColumn<DateOnly>(
                name: "ended_on",
                table: "student_class_enrollments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "started_on",
                table: "student_class_enrollments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existing rows started with their academic year.
            migrationBuilder.Sql(@"
UPDATE student_class_enrollments e
JOIN academic_years ay ON ay.id = e.academic_year_id
SET e.started_on = ay.start_date;");

            migrationBuilder.AddColumn<string>(
                name: "active_code",
                table: "students",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "CASE WHEN status <> 'INACTIVE' THEN code ELSE NULL END",
                stored: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "active_year_key",
                table: "student_class_enrollments",
                type: "bigint unsigned",
                nullable: true,
                computedColumnSql: "CASE WHEN status = 'ACTIVE' THEN academic_year_id ELSE NULL END",
                stored: true);

            migrationBuilder.CreateTable(
                name: "student_import_batches",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    school_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    academic_year_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    source = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "DRAFT", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_content = table.Column<byte[]>(type: "longblob", nullable: false),
                    file_size = table.Column<uint>(type: "int unsigned", nullable: false),
                    total_rows = table.Column<uint>(type: "int unsigned", nullable: false),
                    valid_rows = table.Column<uint>(type: "int unsigned", nullable: false),
                    invalid_rows = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reviewed_by_user_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    review_comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    applied_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_import_batches", x => x.id);
                    table.CheckConstraint("ck_student_import_batches_source", "source IN ('ADMIN', 'SCHOOL')");
                    table.CheckConstraint("ck_student_import_batches_status", "status IN ('DRAFT', 'SUBMITTED', 'REJECTED', 'APPLIED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_student_import_batches_creator",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_import_batches_reviewer",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_student_import_batches_school",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_import_batches_year",
                        column: x => x.academic_year_id,
                        principalTable: "academic_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "student_import_rows",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    row_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    raw_code = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_full_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_date_of_birth = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_gender = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_admission_date = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_class_code = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resolved_school_class_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    is_valid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    error_json = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_student_id = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_import_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_import_rows_batch",
                        column: x => x.batch_id,
                        principalTable: "student_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_import_rows_class",
                        column: x => x.resolved_school_class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_student_import_rows_student",
                        column: x => x.created_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_students_code",
                table: "students",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "uq_students_active_code",
                table: "students",
                column: "active_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_student_enrollments_student_year",
                table: "student_class_enrollments",
                columns: new[] { "student_id", "academic_year_id" });

            migrationBuilder.CreateIndex(
                name: "uq_student_enrollments_student_active_year",
                table: "student_class_enrollments",
                columns: new[] { "student_id", "active_year_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_student_import_batches_creator",
                table: "student_import_batches",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_import_batches_reviewer",
                table: "student_import_batches",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_import_batches_school_status",
                table: "student_import_batches",
                columns: new[] { "school_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_student_import_batches_academic_year_id",
                table: "student_import_batches",
                column: "academic_year_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_import_rows_batch_valid",
                table: "student_import_rows",
                columns: new[] { "batch_id", "is_valid" });

            migrationBuilder.CreateIndex(
                name: "idx_student_import_rows_class",
                table: "student_import_rows",
                column: "resolved_school_class_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_import_rows_student",
                table: "student_import_rows",
                column: "created_student_id");

            migrationBuilder.CreateIndex(
                name: "uq_student_import_rows_batch_row",
                table: "student_import_rows",
                columns: new[] { "batch_id", "row_number" },
                unique: true);

            // Only now that their replacements exist can the old unique indexes go.
            migrationBuilder.DropIndex(
                name: "uq_student_enrollments_student_year",
                table: "student_class_enrollments");

            migrationBuilder.DropIndex(
                name: "uq_students_code",
                table: "students");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Technical rollback only. It restores the old "one enrollment per student and year" and
            // "globally unique student code" rules, so history that violates them is collapsed first.
            migrationBuilder.DropTable(
                name: "student_import_rows");

            migrationBuilder.DropTable(
                name: "student_import_batches");

            // Keep one enrollment per (student, year): the ACTIVE one, otherwise the newest.
            migrationBuilder.Sql(@"
DELETE e FROM student_class_enrollments e
JOIN student_class_enrollments k
  ON k.student_id = e.student_id
 AND k.academic_year_id = e.academic_year_id
 AND k.id <> e.id
WHERE (k.status = 'ACTIVE' AND e.status <> 'ACTIVE')
   OR ((k.status = 'ACTIVE') = (e.status = 'ACTIVE') AND k.id > e.id);");

            // Deleted (INACTIVE) students may share a code with a live one; make theirs distinct.
            migrationBuilder.Sql(@"
UPDATE students s
SET s.code = CONCAT(LEFT(s.code, 50), '#', s.id)
WHERE s.status = 'INACTIVE'
  AND EXISTS (SELECT 1 FROM (SELECT id, code FROM students) o
              WHERE o.code = s.code AND o.id <> s.id);");

            // Restore the old unique indexes before dropping their replacements: the enrollment one
            // backs fk_student_enrollments_student.
            migrationBuilder.CreateIndex(
                name: "uq_student_enrollments_student_year",
                table: "student_class_enrollments",
                columns: new[] { "student_id", "academic_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_students_code",
                table: "students",
                column: "code",
                unique: true);

            migrationBuilder.DropIndex(
                name: "idx_students_code",
                table: "students");

            migrationBuilder.DropIndex(
                name: "uq_students_active_code",
                table: "students");

            migrationBuilder.DropIndex(
                name: "idx_student_enrollments_student_year",
                table: "student_class_enrollments");

            migrationBuilder.DropIndex(
                name: "uq_student_enrollments_student_active_year",
                table: "student_class_enrollments");

            migrationBuilder.DropColumn(
                name: "active_code",
                table: "students");

            migrationBuilder.DropColumn(
                name: "active_year_key",
                table: "student_class_enrollments");

            migrationBuilder.DropColumn(
                name: "ended_on",
                table: "student_class_enrollments");

            migrationBuilder.DropColumn(
                name: "started_on",
                table: "student_class_enrollments");
        }
    }
}
