using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookmarkTableDB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookDocumentId",
                table: "Bookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_BookDocumentId",
                table: "Bookmarks",
                column: "BookDocumentId");

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
                name: "FK_Bookmarks_BookDocuments_BookDocumentId",
                table: "Bookmarks");

            migrationBuilder.DropIndex(
                name: "IX_Bookmarks_BookDocumentId",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "BookDocumentId",
                table: "Bookmarks");
        }
    }
}
