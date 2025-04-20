using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyApp.Migrations
{
    /// <inheritdoc />
    public partial class ImageUrlWord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Lessons",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Lessons");
        }
    }
}
