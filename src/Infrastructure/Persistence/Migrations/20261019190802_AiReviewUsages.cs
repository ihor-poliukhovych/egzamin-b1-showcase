using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiReviewUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_review_usages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Part = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_review_usages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_review_usages_UserId_At",
                table: "ai_review_usages",
                columns: new[] { "UserId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_review_usages");
        }
    }
}
