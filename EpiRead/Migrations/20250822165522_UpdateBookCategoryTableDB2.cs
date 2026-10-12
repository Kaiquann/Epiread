using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookCategoryTableDB2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachment_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscussionAttachment",
                table: "DiscussionAttachment");

            migrationBuilder.RenameTable(
                name: "DiscussionAttachment",
                newName: "DiscussionAttachments");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionAttachment_DiscussionCommentId",
                table: "DiscussionAttachments",
                newName: "IX_DiscussionAttachments_DiscussionCommentId");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BookCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BookCategories",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscussionAttachments",
                table: "DiscussionAttachments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments",
                column: "DiscussionCommentId",
                principalTable: "DiscussionComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiscussionAttachments",
                table: "DiscussionAttachments");

            migrationBuilder.RenameTable(
                name: "DiscussionAttachments",
                newName: "DiscussionAttachment");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionAttachments_DiscussionCommentId",
                table: "DiscussionAttachment",
                newName: "IX_DiscussionAttachment_DiscussionCommentId");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "BookCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "BookCategories",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiscussionAttachment",
                table: "DiscussionAttachment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionAttachment_DiscussionComments_DiscussionCommentId",
                table: "DiscussionAttachment",
                column: "DiscussionCommentId",
                principalTable: "DiscussionComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
