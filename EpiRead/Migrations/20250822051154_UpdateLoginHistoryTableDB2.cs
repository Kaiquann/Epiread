using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLoginHistoryTableDB2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginHistory_Users_UserId",
                table: "LoginHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoginHistory",
                table: "LoginHistory");

            migrationBuilder.RenameTable(
                name: "LoginHistory",
                newName: "LoginHistories");

            migrationBuilder.RenameIndex(
                name: "IX_LoginHistory_UserId",
                table: "LoginHistories",
                newName: "IX_LoginHistories_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoginHistories",
                table: "LoginHistories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginHistories_Users_UserId",
                table: "LoginHistories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginHistories_Users_UserId",
                table: "LoginHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoginHistories",
                table: "LoginHistories");

            migrationBuilder.RenameTable(
                name: "LoginHistories",
                newName: "LoginHistory");

            migrationBuilder.RenameIndex(
                name: "IX_LoginHistories_UserId",
                table: "LoginHistory",
                newName: "IX_LoginHistory_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoginHistory",
                table: "LoginHistory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginHistory_Users_UserId",
                table: "LoginHistory",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
