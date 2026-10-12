using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTableDB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookHistories_BookDocuments_BookDocumentId",
                table: "BookHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_BookDocuments_BookDocumentId",
                table: "Bookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionComments_Users_UserId1",
                table: "DiscussionComments");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionComments_UserId1",
                table: "DiscussionComments");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "DiscussionComments");

            migrationBuilder.AddForeignKey(
                name: "FK_BookHistories_BookDocuments_BookDocumentId",
                table: "BookHistories",
                column: "BookDocumentId",
                principalTable: "BookDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_BookDocuments_BookDocumentId",
                table: "Bookmarks",
                column: "BookDocumentId",
                principalTable: "BookDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookHistories_BookDocuments_BookDocumentId",
                table: "BookHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_BookDocuments_BookDocumentId",
                table: "Bookmarks");

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "DiscussionComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionComments_UserId1",
                table: "DiscussionComments",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BookHistories_BookDocuments_BookDocumentId",
                table: "BookHistories",
                column: "BookDocumentId",
                principalTable: "BookDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_BookDocuments_BookDocumentId",
                table: "Bookmarks",
                column: "BookDocumentId",
                principalTable: "BookDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionComments_Users_UserId1",
                table: "DiscussionComments",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
