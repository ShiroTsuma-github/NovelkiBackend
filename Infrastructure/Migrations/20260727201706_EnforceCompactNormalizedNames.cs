using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCompactNormalizedNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Tags_NormalizedName_NoSpaces",
                table: "Tags",
                sql: "\"NormalizedName\" = replace(\"NormalizedName\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PublicBookSnapshots_NormalizedPrimaryTitle_NoSpaces",
                table: "PublicBookSnapshots",
                sql: "\"NormalizedPrimaryTitle\" = replace(\"NormalizedPrimaryTitle\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Genres_NormalizedName_NoSpaces",
                table: "Genres",
                sql: "\"NormalizedName\" = replace(\"NormalizedName\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookTitles_NormalizedTitle_NoSpaces",
                table: "BookTitles",
                sql: "\"NormalizedTitle\" = replace(\"NormalizedTitle\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Books_NormalizedPrimaryTitle_NoSpaces",
                table: "Books",
                sql: "\"NormalizedPrimaryTitle\" = replace(\"NormalizedPrimaryTitle\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Authors_NormalizedPrimaryName_NoSpaces",
                table: "Authors",
                sql: "\"NormalizedPrimaryName\" = replace(\"NormalizedPrimaryName\", ' ', '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuthorNames_NormalizedName_NoSpaces",
                table: "AuthorNames",
                sql: "\"NormalizedName\" = replace(\"NormalizedName\", ' ', '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tags_NormalizedName_NoSpaces",
                table: "Tags");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PublicBookSnapshots_NormalizedPrimaryTitle_NoSpaces",
                table: "PublicBookSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Genres_NormalizedName_NoSpaces",
                table: "Genres");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookTitles_NormalizedTitle_NoSpaces",
                table: "BookTitles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Books_NormalizedPrimaryTitle_NoSpaces",
                table: "Books");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Authors_NormalizedPrimaryName_NoSpaces",
                table: "Authors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuthorNames_NormalizedName_NoSpaces",
                table: "AuthorNames");
        }
    }
}
