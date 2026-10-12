using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionAttachmentTableDB2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId",
                table: "DiscussionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionPostId",
                table: "DiscussionAttachments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionCommentId",
                table: "DiscussionAttachments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                unique: true,
                filter: "[DiscussionCommentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                principalTable: "DiscussionComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId",
                table: "DiscussionAttachments",
                column: "DiscussionPostId",
                principalTable: "DiscussionPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId",
                table: "DiscussionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionPostId",
                table: "DiscussionAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DiscussionCommentId",
                table: "DiscussionAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                principalTable: "DiscussionComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId",
                table: "DiscussionAttachments",
                column: "DiscussionPostId",
                principalTable: "DiscussionPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }
    }
}
