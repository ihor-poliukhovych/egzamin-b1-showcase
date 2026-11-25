using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B1.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Exam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arkusze",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arkusze", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArkuszId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Points = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    MaxPoints = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_attempts_arkusze_ArkuszId",
                        column: x => x.ArkuszId,
                        principalTable: "arkusze",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArkuszId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_parts_arkusze_ArkuszId",
                        column: x => x.ArkuszId,
                        principalTable: "arkusze",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_answers_exam_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "exam_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    PointsPerItem = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    BasedOn = table.Column<string>(type: "text", nullable: true),
                    Example = table.Column<string>(type: "text", nullable: true),
                    WordBank = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_tasks_exam_parts_PartId",
                        column: x => x.PartId,
                        principalTable: "exam_parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                    Hint = table.Column<string>(type: "text", nullable: true),
                    Options = table.Column<string>(type: "text", nullable: false),
                    AnswerKey = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_items_exam_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "exam_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arkusze_Code",
                table: "arkusze",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_answers_AttemptId_ItemId",
                table: "exam_answers",
                columns: new[] { "AttemptId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_ArkuszId",
                table: "exam_attempts",
                column: "ArkuszId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_UserId_ArkuszId",
                table: "exam_attempts",
                columns: new[] { "UserId", "ArkuszId" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_items_TaskId_Number",
                table: "exam_items",
                columns: new[] { "TaskId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_parts_ArkuszId_Kind",
                table: "exam_parts",
                columns: new[] { "ArkuszId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_tasks_PartId_Number",
                table: "exam_tasks",
                columns: new[] { "PartId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_answers");

            migrationBuilder.DropTable(
                name: "exam_items");

            migrationBuilder.DropTable(
                name: "exam_attempts");

            migrationBuilder.DropTable(
                name: "exam_tasks");

            migrationBuilder.DropTable(
                name: "exam_parts");

            migrationBuilder.DropTable(
                name: "arkusze");
        }
    }
}
