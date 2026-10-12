using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class CreateDiscussionLikeCountTableDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscussionLikeCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiscussionPostId = table.Column<int>(type: "int", nullable: false),
                    DiscussionCommentId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionLikeCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionLikeCounts_DiscussionComments_DiscussionCommentId",
                        column: x => x.DiscussionCommentId,
                        principalTable: "DiscussionComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId",
                        column: x => x.DiscussionPostId,
                        principalTable: "DiscussionPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_DiscussionLikeCounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLikeCounts_DiscussionCommentId",
                table: "DiscussionLikeCounts",
                column: "DiscussionCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLikeCounts_DiscussionPostId",
                table: "DiscussionLikeCounts",
                column: "DiscussionPostId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLikeCounts_UserId",
                table: "DiscussionLikeCounts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscussionLikeCounts");
        }
    }
}
