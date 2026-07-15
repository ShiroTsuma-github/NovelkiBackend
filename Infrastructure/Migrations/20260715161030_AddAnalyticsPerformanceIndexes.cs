using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookProgressHistory_BookId_ChangedAt",
                table: "BookProgressHistory");

            migrationBuilder.DropIndex(
                name: "IX_BookLinks_BookId",
                table: "BookLinks");

            migrationBuilder.CreateIndex(
                name: "IX_BookTitles_BookId_IsPrimary",
                table: "BookTitles",
                columns: new[] { "BookId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_OwnerId_Created",
                table: "Books",
                columns: new[] { "OwnerId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_Books_OwnerId_Priority",
                table: "Books",
                columns: new[] { "OwnerId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_BookProgressHistory_BookId_ChangedAt_Id",
                table: "BookProgressHistory",
                columns: new[] { "BookId", "ChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BookLinks_BookId_SourceType",
                table: "BookLinks",
                columns: new[] { "BookId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_BookCovers_BookId_Status_Source",
                table: "BookCovers",
                columns: new[] { "BookId", "Status", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookTitles_BookId_IsPrimary",
                table: "BookTitles");

            migrationBuilder.DropIndex(
                name: "IX_Books_OwnerId_Created",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_OwnerId_Priority",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookProgressHistory_BookId_ChangedAt_Id",
                table: "BookProgressHistory");

            migrationBuilder.DropIndex(
                name: "IX_BookLinks_BookId_SourceType",
                table: "BookLinks");

            migrationBuilder.DropIndex(
                name: "IX_BookCovers_BookId_Status_Source",
                table: "BookCovers");

            migrationBuilder.CreateIndex(
                name: "IX_BookProgressHistory_BookId_ChangedAt",
                table: "BookProgressHistory",
                columns: new[] { "BookId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookLinks_BookId",
                table: "BookLinks",
                column: "BookId");
        }
    }
}
