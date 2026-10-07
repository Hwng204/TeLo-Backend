using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChaptersLessonsByBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chương/bài không còn gắn với sách giáo khoa: mỗi phân hiệu tự quản lý chương theo
            // (khối, lĩnh vực). Dữ liệu cũ được chuyển sang, nhân bản cho từng phân hiệu đang dùng
            // cùng một sách, và ma trận/nhiệm vụ được trỏ sang bản của đúng phân hiệu.
            // Các bước xoá dữ liệu cũ nằm cuối để dữ liệu lỗi làm migration dừng trước khi mất gì.

            // Kiểm tra dữ liệu trước khi đổi bất cứ thứ gì: MySQL tự commit sau mỗi lệnh DDL, lỗi ở
            // giữa sẽ để DB dở dang và không chạy lại được. Dữ liệu không chuyển được thì dừng ở đây,
            // sửa dữ liệu rồi chạy lại.
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS preflight_chapters_lessons;");
            migrationBuilder.Sql("""
                CREATE PROCEDURE preflight_chapters_lessons()
                BEGIN
                  -- uq_academic_contexts_scope mới không còn textbook_id.
                  IF EXISTS (SELECT 1 FROM academic_contexts
                             GROUP BY academic_year_id, school_id, school_branch_id, subject_id, grade_level_id
                             HAVING COUNT(*) > 1) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
                      'ChaptersLessonsByBranch: one year, branch, subject and grade has contexts for two textbooks';
                  END IF;

                  -- Mỗi chương nằm ở mọi (phân hiệu, khối, môn) đang dùng sách của nó; mã là C + thứ tự.
                  IF EXISTS (WITH placed AS (
                               SELECT DISTINCT ac.school_branch_id AS b, ac.grade_level_id AS g, ac.subject_id AS s,
                                      c.id, c.sort_order, c.title COLLATE utf8mb4_0900_as_ci AS title
                               FROM textbook_chapters c
                               JOIN academic_contexts ac ON ac.textbook_id = c.textbook_id)
                             SELECT 1 FROM placed GROUP BY b, g, s, sort_order HAVING COUNT(*) > 1
                             UNION ALL
                             SELECT 1 FROM placed GROUP BY b, g, s, title HAVING COUNT(*) > 1) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
                      'ChaptersLessonsByBranch: two chapters would get the same code or title in one branch, grade and subject';
                  END IF;

                  IF EXISTS (SELECT 1 FROM textbook_lessons l
                             JOIN textbook_chapters c ON c.id = l.chapter_id
                             WHERE EXISTS (SELECT 1 FROM academic_contexts ac WHERE ac.textbook_id = c.textbook_id)
                             GROUP BY l.chapter_id, l.title COLLATE utf8mb4_0900_as_ci HAVING COUNT(*) > 1) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
                      'ChaptersLessonsByBranch: two lessons of one chapter have the same title';
                  END IF;

                  -- Bài dùng trong ma trận/nhiệm vụ phải thuộc sách của ngữ cảnh, nếu không sẽ không có
                  -- bản ở đúng phân hiệu để trỏ sang.
                  IF EXISTS (SELECT 1 FROM matrix_details d
                             JOIN exam_matrices m ON m.id = d.exam_matrix_id
                             JOIN academic_contexts ac ON ac.id = m.academic_context_id
                             JOIN textbook_lessons l ON l.id = d.lesson_id
                             JOIN textbook_chapters c ON c.id = l.chapter_id
                             WHERE c.textbook_id <> ac.textbook_id
                             UNION ALL
                             SELECT 1 FROM question_tasks q
                             JOIN tasks t ON t.id = q.task_id
                             JOIN academic_contexts ac ON ac.id = t.academic_context_id
                             JOIN textbook_lessons l ON l.id = q.lesson_id
                             JOIN textbook_chapters c ON c.id = l.chapter_id
                             WHERE c.textbook_id <> ac.textbook_id) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
                      'ChaptersLessonsByBranch: a matrix or question task uses a lesson outside its context textbook';
                  END IF;
                END;
                """);
            migrationBuilder.Sql("CALL preflight_chapters_lessons();");
            migrationBuilder.Sql("DROP PROCEDURE preflight_chapters_lessons;");

            // matrix_details và question_tasks sẽ được trỏ sang bảng lessons mới.
            migrationBuilder.DropForeignKey(
                name: "fk_matrix_details_lesson",
                table: "matrix_details");

            migrationBuilder.DropForeignKey(
                name: "fk_question_tasks_lesson",
                table: "question_tasks");

            migrationBuilder.CreateTable(
                name: "subject_fields",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    subject_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "ACTIVE", collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_fields_subject",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "uq_subject_fields_name",
                table: "subject_fields",
                columns: new[] { "subject_id", "name" },
                unique: true);


            migrationBuilder.Sql("""
                INSERT INTO subject_fields (subject_id, name, status)
                SELECT id, 'Số và phép tính', 'ACTIVE' FROM subjects WHERE name = 'Toán'
                UNION ALL
                SELECT id, 'Hình học và đo lường', 'ACTIVE' FROM subjects WHERE name = 'Toán';
                """);

            // Môn đang có dữ liệu cũ mà chưa có lĩnh vực nào thì tạm xếp vào "Chưa phân loại".
            migrationBuilder.Sql("""
                INSERT INTO subject_fields (subject_id, name, status)
                SELECT DISTINCT ac.subject_id, 'Chưa phân loại', 'ACTIVE'
                FROM academic_contexts ac
                WHERE NOT EXISTS (SELECT 1 FROM subject_fields f WHERE f.subject_id = ac.subject_id);
                """);

            migrationBuilder.CreateTable(
                name: "chapters",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    school_branch_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    grade_level_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    field_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_as_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_as_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapters", x => x.id);
                    table.ForeignKey(
                        name: "fk_chapters_branch",
                        column: x => x.school_branch_id,
                        principalTable: "school_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chapters_field",
                        column: x => x.field_id,
                        principalTable: "subject_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chapters_grade",
                        column: x => x.grade_level_id,
                        principalTable: "grade_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    chapter_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_as_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "utf8mb4_0900_as_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lessons", x => x.id);
                    table.ForeignKey(
                        name: "fk_lessons_chapter",
                        column: x => x.chapter_id,
                        principalTable: "chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "idx_chapters_field",
                table: "chapters",
                column: "field_id");

            migrationBuilder.CreateIndex(
                name: "idx_chapters_grade",
                table: "chapters",
                column: "grade_level_id");

            migrationBuilder.CreateIndex(
                name: "uq_chapters_code",
                table: "chapters",
                columns: new[] { "school_branch_id", "grade_level_id", "field_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_chapters_title",
                table: "chapters",
                columns: new[] { "school_branch_id", "grade_level_id", "field_id", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_lessons_code",
                table: "lessons",
                columns: new[] { "chapter_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_lessons_title",
                table: "lessons",
                columns: new[] { "chapter_id", "title" },
                unique: true);

            migrationBuilder.Sql("ALTER TABLE chapters ADD COLUMN source_chapter_id bigint unsigned NULL;");
            migrationBuilder.Sql("ALTER TABLE lessons ADD COLUMN source_lesson_id bigint unsigned NULL;");

            // Bản chính của mỗi chương giữ nguyên id. Phân hiệu, khối, môn lấy từ ngữ cảnh học có id
            // nhỏ nhất đang dùng sách đó; lĩnh vực tạm là lĩnh vực đầu tiên của môn. Sách không có
            // ngữ cảnh học nào thì không có phân hiệu để gán nên chương của nó bị bỏ.
            migrationBuilder.Sql("""
                INSERT INTO chapters (id, school_branch_id, grade_level_id, field_id, code, title, sort_order)
                SELECT c.id, ac.school_branch_id, ac.grade_level_id, f.field_id,
                       CONCAT('C', c.sort_order), c.title, c.sort_order
                FROM textbook_chapters c
                JOIN academic_contexts ac
                  ON ac.id = (SELECT MIN(x.id) FROM academic_contexts x WHERE x.textbook_id = c.textbook_id)
                JOIN (SELECT subject_id, MIN(id) AS field_id FROM subject_fields GROUP BY subject_id) f
                  ON f.subject_id = ac.subject_id;
                """);

            // Bản sao cho mỗi (phân hiệu, khối, môn) khác cùng dùng sách đó.
            migrationBuilder.Sql("""
                INSERT INTO chapters (school_branch_id, grade_level_id, field_id, code, title, sort_order, source_chapter_id)
                SELECT g.school_branch_id, g.grade_level_id, f.field_id, p.code, p.title, p.sort_order, p.id
                FROM textbook_chapters c
                JOIN chapters p ON p.id = c.id
                JOIN (SELECT DISTINCT textbook_id, school_branch_id, grade_level_id, subject_id FROM academic_contexts) g
                  ON g.textbook_id = c.textbook_id
                JOIN (SELECT subject_id, MIN(id) AS field_id FROM subject_fields GROUP BY subject_id) f
                  ON f.subject_id = g.subject_id
                WHERE NOT (g.school_branch_id = p.school_branch_id
                       AND g.grade_level_id = p.grade_level_id
                       AND f.field_id = p.field_id);
                """);

            migrationBuilder.Sql("""
                INSERT INTO lessons (id, chapter_id, code, title, content, sort_order)
                SELECT l.id, l.chapter_id, CONCAT('B', l.sort_order), l.title, l.content, l.sort_order
                FROM textbook_lessons l
                JOIN chapters c ON c.id = l.chapter_id AND c.source_chapter_id IS NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO lessons (chapter_id, code, title, content, sort_order, source_lesson_id)
                SELECT c.id, CONCAT('B', l.sort_order), l.title, l.content, l.sort_order, l.id
                FROM textbook_lessons l
                JOIN chapters c ON c.source_chapter_id = l.chapter_id;
                """);

            // Ma trận và nhiệm vụ của phân hiệu khác bản chính được trỏ sang bài trong bản sao của
            // đúng phân hiệu đó.
            migrationBuilder.Sql("""
                UPDATE matrix_details d
                JOIN exam_matrices m ON m.id = d.exam_matrix_id
                JOIN academic_contexts ac ON ac.id = m.academic_context_id
                JOIN lessons l ON l.source_lesson_id = d.lesson_id
                JOIN chapters c ON c.id = l.chapter_id
                 AND c.school_branch_id = ac.school_branch_id
                 AND c.grade_level_id = ac.grade_level_id
                JOIN subject_fields f ON f.id = c.field_id AND f.subject_id = ac.subject_id
                SET d.lesson_id = l.id;
                """);

            migrationBuilder.Sql("""
                UPDATE question_tasks q
                JOIN tasks t ON t.id = q.task_id
                JOIN academic_contexts ac ON ac.id = t.academic_context_id
                JOIN lessons l ON l.source_lesson_id = q.lesson_id
                JOIN chapters c ON c.id = l.chapter_id
                 AND c.school_branch_id = ac.school_branch_id
                 AND c.grade_level_id = ac.grade_level_id
                JOIN subject_fields f ON f.id = c.field_id AND f.subject_id = ac.subject_id
                SET q.lesson_id = l.id;
                """);

            migrationBuilder.Sql("ALTER TABLE chapters DROP COLUMN source_chapter_id;");
            migrationBuilder.Sql("ALTER TABLE lessons DROP COLUMN source_lesson_id;");

            // Còn dòng nào trỏ vào bài không chuyển được (sách không có ngữ cảnh học nào) thì hai khoá
            // ngoại dưới đây báo lỗi, trước khi bảng cũ bị xoá.
            migrationBuilder.AddForeignKey(
                name: "fk_matrix_details_lesson",
                table: "matrix_details",
                column: "lesson_id",
                principalTable: "lessons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_question_tasks_lesson",
                table: "question_tasks",
                column: "lesson_id",
                principalTable: "lessons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // academic_contexts bỏ textbook_id. Khoá ngoại năm học đang dùng uq_academic_contexts_scope
            // làm chỉ mục, nên cần một chỉ mục tạm trong lúc dựng lại.
            migrationBuilder.DropForeignKey(
                name: "fk_academic_contexts_textbook",
                table: "academic_contexts");

            migrationBuilder.DropIndex(
                name: "IX_academic_contexts_textbook_id",
                table: "academic_contexts");

            migrationBuilder.CreateIndex(
                name: "tmp_academic_contexts_year",
                table: "academic_contexts",
                column: "academic_year_id");

            migrationBuilder.DropIndex(
                name: "uq_academic_contexts_scope",
                table: "academic_contexts");

            migrationBuilder.DropColumn(
                name: "textbook_id",
                table: "academic_contexts");

            migrationBuilder.CreateIndex(
                name: "uq_academic_contexts_scope",
                table: "academic_contexts",
                columns: new[] { "academic_year_id", "school_id", "school_branch_id", "subject_id", "grade_level_id" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "tmp_academic_contexts_year",
                table: "academic_contexts");

            migrationBuilder.DropTable(
                name: "textbook_lessons");

            migrationBuilder.DropTable(
                name: "textbook_chapters");

            migrationBuilder.DropTable(
                name: "textbooks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ponytail: không tự hoàn tác vì chương đã được nhân bản theo phân hiệu và không còn biết
            // sách gốc. Muốn quay lại thì khôi phục DB từ bản sao lưu.
            throw new NotSupportedException(
                "ChaptersLessonsByBranch cannot be reverted automatically; restore the database from a backup.");
        }
    }
}
