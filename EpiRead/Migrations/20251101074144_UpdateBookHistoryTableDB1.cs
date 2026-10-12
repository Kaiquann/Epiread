using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookHistoryTableDB1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentPage",
                table: "BookHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPages",
                table: "BookHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPage",
                table: "BookHistories");

            migrationBuilder.DropColumn(
                name: "TotalPages",
                table: "BookHistories");
        }
    }
}
