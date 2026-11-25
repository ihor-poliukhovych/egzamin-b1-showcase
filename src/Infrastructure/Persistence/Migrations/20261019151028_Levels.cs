using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Levels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetLevel",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetVariant",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPointsOverride",
                table: "exam_parts",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "arkusze",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "arkusze",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetLevel",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TargetVariant",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MaxPointsOverride",
                table: "exam_parts");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "arkusze");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "arkusze");
        }
    }
}
