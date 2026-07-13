using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookCoverThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThumbnailHeight",
                table: "BookCovers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailMimeType",
                table: "BookCovers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ThumbnailSizeBytes",
                table: "BookCovers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStoragePath",
                table: "BookCovers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThumbnailWidth",
                table: "BookCovers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailHeight",
                table: "BookCovers");

            migrationBuilder.DropColumn(
                name: "ThumbnailMimeType",
                table: "BookCovers");

            migrationBuilder.DropColumn(
                name: "ThumbnailSizeBytes",
                table: "BookCovers");

            migrationBuilder.DropColumn(
                name: "ThumbnailStoragePath",
                table: "BookCovers");

            migrationBuilder.DropColumn(
                name: "ThumbnailWidth",
                table: "BookCovers");
        }
    }
}
