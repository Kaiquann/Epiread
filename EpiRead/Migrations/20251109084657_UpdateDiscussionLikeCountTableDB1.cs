using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionLikeCountTableDB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId",
                table: "DiscussionLikeCounts");

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionPostId",
                table: "DiscussionLikeCounts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId",
                table: "DiscussionLikeCounts",
                column: "DiscussionPostId",
                principalTable: "DiscussionPosts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId",
                table: "DiscussionLikeCounts");

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionPostId",
                table: "DiscussionLikeCounts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId",
                table: "DiscussionLikeCounts",
                column: "DiscussionPostId",
                principalTable: "DiscussionPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
