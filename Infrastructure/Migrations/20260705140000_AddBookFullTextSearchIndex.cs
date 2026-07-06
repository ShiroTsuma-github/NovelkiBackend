using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddBookFullTextSearchIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Books_FullTextSearch"
                ON "Books"
                USING GIN (
                    to_tsvector(
                        'simple',
                        coalesce("PrimaryTitle", '') || ' ' ||
                        coalesce("Description", '') || ' ' ||
                        coalesce("Notes", '')
                    )
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Books_FullTextSearch";""");
        }
    }
}
