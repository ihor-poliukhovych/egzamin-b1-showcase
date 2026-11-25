using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialErrorBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "error_patterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    WrongExample = table.Column<string>(type: "text", nullable: false),
                    CorrectForm = table.Column<string>(type: "text", nullable: false),
                    Rule = table.Column<string>(type: "text", nullable: false),
                    ContrastRuUa = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FallsCount = table.Column<int>(type: "integer", nullable: false),
                    CleanStreak = table.Column<int>(type: "integer", nullable: false),
                    NextDueAt = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFailDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastCleanPassDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_patterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DurationSec = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Result = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    CountsTowardStreak = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attempts_error_patterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "error_patterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_items_review_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "review_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attempts_PatternId_Date",
                table: "attempts",
                columns: new[] { "PatternId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_error_patterns_UserId_Code",
                table: "error_patterns",
                columns: new[] { "UserId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_error_patterns_UserId_Status",
                table: "error_patterns",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_review_items_SessionId_PatternId",
                table: "review_items",
                columns: new[] { "SessionId", "PatternId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_review_sessions_UserId_Date",
                table: "review_sessions",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropTable(
                name: "review_items");

            migrationBuilder.DropTable(
                name: "error_patterns");

            migrationBuilder.DropTable(
                name: "review_sessions");
        }
    }
}
