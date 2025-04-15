using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyApp.Migrations
{
    /// <inheritdoc />
    public partial class ModelUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Example",
                table: "Words",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Mean",
                table: "Words",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Example",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Mean",
                table: "Words");
        }
    }
}
