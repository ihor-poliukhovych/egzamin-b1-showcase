using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiUsageKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ai_review_usages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // rows written before this migration were all Pisanie/Mowienie reviews
                defaultValue: "Review");

            migrationBuilder.CreateIndex(
                name: "IX_ai_review_usages_UserId_Kind_At",
                table: "ai_review_usages",
                columns: new[] { "UserId", "Kind", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_review_usages_UserId_Kind_At",
                table: "ai_review_usages");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ai_review_usages");
        }
    }
}
