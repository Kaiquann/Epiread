using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionCommentTableDB3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId",
                table: "DiscussionComments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionComments_DiscussionAttachmentId",
                table: "DiscussionComments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.DropColumn(
                name: "DiscussionAttachmentId",
                table: "DiscussionComments");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.AddColumn<int>(
                name: "DiscussionAttachmentId",
                table: "DiscussionComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionComments_DiscussionAttachmentId",
                table: "DiscussionComments",
                column: "DiscussionAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId",
                table: "DiscussionComments",
                column: "DiscussionAttachmentId",
                principalTable: "DiscussionAttachments",
                principalColumn: "Id");
        }
    }
}
