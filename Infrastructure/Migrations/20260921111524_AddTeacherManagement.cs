using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "security_version",
                table: "users",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "teachers",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "employment_status",
                table: "teachers",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "WORKING",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "joined_on",
                table: "teachers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "main_subject_id",
                table: "teachers",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "staff_code",
                table: "teachers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<uint>(
                name: "version",
                table: "teachers",
                type: "int unsigned",
                nullable: false,
                defaultValue: 1u);

            migrationBuilder.CreateIndex(
                name: "IX_teachers_employment_status_department",
                table: "teachers",
                columns: new[] { "employment_status", "department" });

            migrationBuilder.CreateIndex(
                name: "IX_teachers_main_subject_id",
                table: "teachers",
                column: "main_subject_id");

            migrationBuilder.CreateIndex(
                name: "uq_teachers_staff_code",
                table: "teachers",
                column: "staff_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_teachers_subjects_main_subject_id",
                table: "teachers",
                column: "main_subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teachers_subjects_main_subject_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "IX_teachers_employment_status_department",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "IX_teachers_main_subject_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "uq_teachers_staff_code",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "security_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "employment_status",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "joined_on",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "main_subject_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "staff_code",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "version",
                table: "teachers");
        }
    }
}
