using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookHistoryTableDB3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BookHistories_BookDocumentId",
                table: "BookHistories",
                column: "BookDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookHistories_BookDocuments_BookDocumentId",
                table: "BookHistories",
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

            migrationBuilder.DropIndex(
                name: "IX_BookHistories_BookDocumentId",
                table: "BookHistories");
        }
    }
}
