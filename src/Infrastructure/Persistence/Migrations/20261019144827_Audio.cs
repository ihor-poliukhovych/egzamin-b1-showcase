using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Audio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaysAllowed",
                table: "exam_tasks");

            migrationBuilder.DropColumn(
                name: "PlayLog",
                table: "exam_attempts");

            migrationBuilder.AddColumn<string>(
                name: "AudioKey",
                table: "exam_parts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AudioPositionSec",
                table: "exam_attempts",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioKey",
                table: "exam_parts");

            migrationBuilder.DropColumn(
                name: "AudioPositionSec",
                table: "exam_attempts");

            migrationBuilder.AddColumn<int>(
                name: "PlaysAllowed",
                table: "exam_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlayLog",
                table: "exam_attempts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
