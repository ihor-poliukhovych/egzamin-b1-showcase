using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "exam_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArkuszId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_sessions_arkusze_ArkuszId",
                        column: x => x.ArkuszId,
                        principalTable: "arkusze",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_SessionId",
                table: "exam_attempts",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_ArkuszId",
                table: "exam_sessions",
                column: "ArkuszId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_UserId_ArkuszId",
                table: "exam_sessions",
                columns: new[] { "UserId", "ArkuszId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_sessions");

            migrationBuilder.DropIndex(
                name: "IX_exam_attempts_SessionId",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "exam_attempts");
        }
    }
}
