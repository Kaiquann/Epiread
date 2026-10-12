using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionAttachmentTableDB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "DiscussionAttachments");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "DiscussionAttachments");

            migrationBuilder.AddColumn<int>(
                name: "DiscussionPostId",
                table: "DiscussionAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FileLink",
                table: "DiscussionAttachments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "DiscussionAttachments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionAttachments_DiscussionPostId",
                table: "DiscussionAttachments",
                column: "DiscussionPostId");

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
                name: "FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId",
                table: "DiscussionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionAttachments_DiscussionPostId",
                table: "DiscussionAttachments");

            migrationBuilder.DropColumn(
                name: "DiscussionPostId",
                table: "DiscussionAttachments");

            migrationBuilder.DropColumn(
                name: "FileLink",
                table: "DiscussionAttachments");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "DiscussionAttachments");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "DiscussionAttachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "DiscussionAttachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
