using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetCore_Mvc_Setup_Kit.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookCategoryTableDB4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "BookCategories");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BookCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "BookCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BookCategories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
