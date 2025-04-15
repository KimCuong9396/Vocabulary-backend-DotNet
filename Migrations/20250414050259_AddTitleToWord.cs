using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleToWord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Words",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Words");
        }
    }
}
