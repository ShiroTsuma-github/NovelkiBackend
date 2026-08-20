using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingBookCoverUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingUploadToken",
                table: "BookCovers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookCovers_PendingUploadToken",
                table: "BookCovers",
                column: "PendingUploadToken",
                unique: true,
                filter: "\"PendingUploadToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookCovers_PendingUploadToken",
                table: "BookCovers");

            migrationBuilder.DropColumn(
                name: "PendingUploadToken",
                table: "BookCovers");
        }
    }
}
