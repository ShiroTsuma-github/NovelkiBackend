using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookTitleTypeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_OwnerId_NormalizedPrimaryTitle",
                table: "Books");

            migrationBuilder.CreateIndex(
                name: "IX_Books_OwnerId_NormalizedPrimaryTitle_ContentTypeId",
                table: "Books",
                columns: new[] { "OwnerId", "NormalizedPrimaryTitle", "ContentTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_OwnerId_NormalizedPrimaryTitle_ContentTypeId",
                table: "Books");

            migrationBuilder.CreateIndex(
                name: "IX_Books_OwnerId_NormalizedPrimaryTitle",
                table: "Books",
                columns: new[] { "OwnerId", "NormalizedPrimaryTitle" });
        }
    }
}
