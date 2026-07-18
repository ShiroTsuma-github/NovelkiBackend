using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Authors_NormalizedPrimaryName",
                table: "Authors");

            migrationBuilder.DropIndex(
                name: "IX_AuthorNames_AuthorId",
                table: "AuthorNames");

            migrationBuilder.DropIndex(
                name: "IX_AuthorNames_NormalizedName",
                table: "AuthorNames");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Authors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Authors",
                type: "uuid",
                nullable: true);

            // Preserve the globally shared behavior of authors that existed before ownership was introduced.
            migrationBuilder.Sql("""
                                 UPDATE "Authors"
                                 SET "OwnerId" = "CreatedBy", "IsPublic" = TRUE;
                                 """);

            migrationBuilder.CreateIndex(
                name: "IX_Authors_NormalizedPrimaryName",
                table: "Authors",
                column: "NormalizedPrimaryName",
                unique: true,
                filter: "\"IsPublic\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Authors_OwnerId_NormalizedPrimaryName",
                table: "Authors",
                columns: new[] { "OwnerId", "NormalizedPrimaryName" },
                unique: true,
                filter: "\"IsPublic\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorNames_AuthorId_NormalizedName",
                table: "AuthorNames",
                columns: new[] { "AuthorId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Authors_AspNetUsers_OwnerId",
                table: "Authors",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Authors_AspNetUsers_OwnerId",
                table: "Authors");

            migrationBuilder.DropIndex(
                name: "IX_Authors_NormalizedPrimaryName",
                table: "Authors");

            migrationBuilder.DropIndex(
                name: "IX_Authors_OwnerId_NormalizedPrimaryName",
                table: "Authors");

            migrationBuilder.DropIndex(
                name: "IX_AuthorNames_AuthorId_NormalizedName",
                table: "AuthorNames");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Authors");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Authors");

            migrationBuilder.CreateIndex(
                name: "IX_Authors_NormalizedPrimaryName",
                table: "Authors",
                column: "NormalizedPrimaryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthorNames_AuthorId",
                table: "AuthorNames",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorNames_NormalizedName",
                table: "AuthorNames",
                column: "NormalizedName",
                unique: true);
        }
    }
}
