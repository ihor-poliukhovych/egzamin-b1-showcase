using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Plays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlaysAllowed",
                table: "exam_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Transcript",
                table: "exam_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayLog",
                table: "exam_attempts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaysAllowed",
                table: "exam_tasks");

            migrationBuilder.DropColumn(
                name: "Transcript",
                table: "exam_tasks");

            migrationBuilder.DropColumn(
                name: "PlayLog",
                table: "exam_attempts");
        }
    }
}
