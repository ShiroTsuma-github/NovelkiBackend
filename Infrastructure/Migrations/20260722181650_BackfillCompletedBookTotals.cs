using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCompletedBookTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Books"
                SET "TotalChapters" = "CurrentChapterNumber"
                WHERE "TotalChapters" IS NULL
                  AND "CurrentChapterNumber" IS NOT NULL
                  AND "CurrentChapterNumber" > 0
                  AND EXISTS (
                      SELECT 1
                      FROM "Statuses"
                      WHERE "Statuses"."Id" = "Books"."StatusId"
                        AND LOWER("Statuses"."Slug") = 'completed'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This data repair cannot distinguish migrated values from totals entered manually later.
        }
    }
}
