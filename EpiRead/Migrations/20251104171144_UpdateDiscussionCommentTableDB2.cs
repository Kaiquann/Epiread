using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionCommentTableDB2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscussionAttachmentId",
                table: "DiscussionComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionComments_DiscussionAttachmentId",
                table: "DiscussionComments",
                column: "DiscussionAttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId",
                table: "DiscussionComments",
                column: "DiscussionAttachmentId",
                principalTable: "DiscussionAttachments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId",
                table: "DiscussionComments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionComments_DiscussionAttachmentId",
                table: "DiscussionComments");

            migrationBuilder.DropColumn(
                name: "DiscussionAttachmentId",
                table: "DiscussionComments");
        }
    }
}
