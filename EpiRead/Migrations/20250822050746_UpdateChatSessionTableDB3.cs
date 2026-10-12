using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChatSessionTableDB3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionsId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ChatSessionsId",
                table: "ChatMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "ChatSessionId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatSessionId",
                table: "ChatMessages",
                column: "ChatSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                table: "ChatMessages",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ChatSessionId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ChatSessionId",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatSessionsId",
                table: "ChatMessages",
                column: "ChatSessionsId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatSessions_ChatSessionsId",
                table: "ChatMessages",
                column: "ChatSessionsId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
