using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDiscussionModerationActionTableDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussionModerationActions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscussionModerationActions",
                columns: table => new
                {
                    DiscussionModerationActionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscussionCommentId = table.Column<int>(type: "int", nullable: true),
                    DiscussionPostId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRemoved = table.Column<bool>(type: "bit", nullable: false),
                    PercentageScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThresholdValue = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionModerationActions", x => x.DiscussionModerationActionId);
                    table.ForeignKey(
                        name: "FK_DiscussionModerationActions_DiscussionComments_DiscussionCommentId",
                        column: x => x.DiscussionCommentId,
                        principalTable: "DiscussionComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DiscussionModerationActions_DiscussionPosts_DiscussionPostId",
                        column: x => x.DiscussionPostId,
                        principalTable: "DiscussionPosts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DiscussionModerationActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionModerationActions_DiscussionCommentId",
                table: "DiscussionModerationActions",
                column: "DiscussionCommentId",
                unique: true,
                filter: "[DiscussionCommentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionModerationActions_DiscussionPostId",
                table: "DiscussionModerationActions",
                column: "DiscussionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionModerationActions_UserId",
                table: "DiscussionModerationActions",
                column: "UserId");
        }
    }
}
