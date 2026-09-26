using Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260926120000_AcademicYearSystemScope")]
public sealed class AcademicYearSystemScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_academic_years_province",
            table: "academic_years");

        migrationBuilder.DropIndex(
            name: "uq_academic_years_active_province",
            table: "academic_years");

        migrationBuilder.DropIndex(
            name: "uq_academic_years_province_name",
            table: "academic_years");

        migrationBuilder.DropColumn(
            name: "active_province_code",
            table: "academic_years");

        migrationBuilder.DropColumn(
            name: "province_code",
            table: "academic_years");

                migrationBuilder.Sql("""
                        UPDATE academic_years
                        SET status = 'CLOSED'
                        WHERE status = 'ACTIVE'
                            AND id <> (
                                SELECT keep_id
                                FROM (
                                    SELECT id AS keep_id
                                    FROM academic_years
                                    WHERE status = 'ACTIVE'
                                    ORDER BY start_date DESC, id DESC
                                    LIMIT 1
                                ) AS active_year
                            );
                        """);

                migrationBuilder.Sql("""
            UPDATE semesters AS semester
            INNER JOIN academic_years AS academic_year
              ON academic_year.id = semester.academic_year_id
            SET semester.status = 'CLOSED'
            WHERE academic_year.status = 'CLOSED';
            """);

        migrationBuilder.AddColumn<string>(
            name: "active_system_key",
            table: "academic_years",
            type: "varchar(16)",
            maxLength: 16,
            nullable: true,
            computedColumnSql: "CASE WHEN status = 'ACTIVE' THEN 'SYSTEM' ELSE NULL END",
            stored: true,
            collation: "utf8mb4_0900_ai_ci")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "ix_academic_years_name",
            table: "academic_years",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "uq_academic_years_active_system",
            table: "academic_years",
            column: "active_system_key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "uq_academic_years_active_system",
            table: "academic_years");

        migrationBuilder.DropIndex(
            name: "ix_academic_years_name",
            table: "academic_years");

        migrationBuilder.DropColumn(
            name: "active_system_key",
            table: "academic_years");

        migrationBuilder.AddColumn<string>(
            name: "province_code",
            table: "academic_years",
            type: "varchar(2)",
            maxLength: 2,
            nullable: true,
            collation: "utf8mb4_0900_ai_ci")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "active_province_code",
            table: "academic_years",
            type: "varchar(2)",
            maxLength: 2,
            nullable: true,
            computedColumnSql: "CASE WHEN status = 'ACTIVE' THEN province_code ELSE NULL END",
            stored: true,
            collation: "utf8mb4_0900_ai_ci")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "uq_academic_years_province_name",
            table: "academic_years",
            columns: new[] { "province_code", "name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_academic_years_active_province",
            table: "academic_years",
            column: "active_province_code",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_academic_years_province",
            table: "academic_years",
            column: "province_code",
            principalTable: "provinces",
            principalColumn: "code",
            onDelete: ReferentialAction.Restrict);
    }
}