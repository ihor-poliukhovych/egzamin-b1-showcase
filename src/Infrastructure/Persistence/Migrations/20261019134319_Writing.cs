using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Writing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetWords",
                table: "exam_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentJson",
                table: "exam_attempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChosenSet",
                table: "exam_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewJson",
                table: "exam_attempts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetWords",
                table: "exam_items");

            migrationBuilder.DropColumn(
                name: "AssessmentJson",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "ChosenSet",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "ReviewJson",
                table: "exam_attempts");
        }
    }
}
