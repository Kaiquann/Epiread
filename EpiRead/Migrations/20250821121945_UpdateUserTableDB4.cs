using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserTableDB4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProfileImageUrl",
                table: "Users",
                newName: "ProfileImagePath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProfileImagePath",
                table: "Users",
                newName: "ProfileImageUrl");
        }
    }
}
