using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterLessonNumber : Migration
    {
        // Hand-ordered: the old title uniques back fk_chapter_branch_id and fk_lesson_chapter_id, so MySQL refuses
        // to drop them (error 1553) until the new indexes with the same leading column exist. One schema change per
        // statement and a VIRTUAL generated column keep every step valid on TiDB too.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "number",
                table: "chapter",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "number",
                table: "lesson",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            // Existing rows keep the order the screens showed (by id): chapters within branch, grade and subject,
            // lessons within their chapter.
            migrationBuilder.Sql(
                "UPDATE `chapter` c JOIN (SELECT `chapter_id`, ROW_NUMBER() OVER (PARTITION BY `branch_id`, `grade_level_id`, `subject_id` ORDER BY `chapter_id`) AS n FROM `chapter`) r ON r.`chapter_id` = c.`chapter_id` SET c.`number` = r.n;");

            migrationBuilder.Sql(
                "UPDATE `lesson` l JOIN (SELECT `lesson_id`, ROW_NUMBER() OVER (PARTITION BY `chapter_id` ORDER BY `lesson_id`) AS n FROM `lesson`) r ON r.`lesson_id` = l.`lesson_id` SET l.`number` = r.n;");

            migrationBuilder.AddColumn<byte>(
                name: "active_flag",
                table: "chapter",
                type: "tinyint unsigned",
                nullable: true,
                computedColumnSql: "(case when `status` = 'ACTIVE' then 1 end)",
                stored: false);

            migrationBuilder.AddColumn<byte>(
                name: "active_flag",
                table: "lesson",
                type: "tinyint unsigned",
                nullable: true,
                computedColumnSql: "(case when `status` = 'ACTIVE' then 1 end)",
                stored: false);

            migrationBuilder.CreateIndex(
                name: "uq_chapter_active_number",
                table: "chapter",
                columns: new[] { "branch_id", "subject_id", "grade_level_id", "number", "active_flag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_chapter_active_title",
                table: "chapter",
                columns: new[] { "branch_id", "subject_id", "grade_level_id", "title", "active_flag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_lesson_active_number",
                table: "lesson",
                columns: new[] { "chapter_id", "number", "active_flag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_lesson_active_title",
                table: "lesson",
                columns: new[] { "chapter_id", "title", "active_flag" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "uq_chapter_branch_id_subject_id_grade_level_id_title",
                table: "chapter");

            migrationBuilder.DropIndex(
                name: "uq_lesson_chapter_id_title",
                table: "lesson");
        }

        // The old title uniques come back first (they back the foreign keys again); this fails while an archived
        // row shares its title with another row of the same scope, which then has to be renamed or deleted first.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "uq_chapter_branch_id_subject_id_grade_level_id_title",
                table: "chapter",
                columns: new[] { "branch_id", "subject_id", "grade_level_id", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_lesson_chapter_id_title",
                table: "lesson",
                columns: new[] { "chapter_id", "title" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "uq_chapter_active_number",
                table: "chapter");

            migrationBuilder.DropIndex(
                name: "uq_chapter_active_title",
                table: "chapter");

            migrationBuilder.DropIndex(
                name: "uq_lesson_active_number",
                table: "lesson");

            migrationBuilder.DropIndex(
                name: "uq_lesson_active_title",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "active_flag",
                table: "chapter");

            migrationBuilder.DropColumn(
                name: "active_flag",
                table: "lesson");

            migrationBuilder.DropColumn(
                name: "number",
                table: "chapter");

            migrationBuilder.DropColumn(
                name: "number",
                table: "lesson");
        }
    }
}
