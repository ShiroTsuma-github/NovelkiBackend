using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingActivityAndTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastProgressUpdatedAt",
                table: "Books",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Books"
                SET "LastProgressUpdatedAt" = COALESCE("LastModified", "Created")
                WHERE "LastProgressUpdatedAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastProgressUpdatedAt",
                table: "Books",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ReadingTimeSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinutesPerChapter = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingTimeSettings", x => x.Id);
                    table.CheckConstraint("CK_ReadingTimeSettings_MinutesPerChapter_Range", "CAST(\"MinutesPerChapter\" AS REAL) >= 0 AND CAST(\"MinutesPerChapter\" AS REAL) <= 1440");
                    table.ForeignKey(
                        name: "FK_ReadingTimeSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingTimeSettings_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_OwnerId_LastProgressUpdatedAt",
                table: "Books",
                columns: new[] { "OwnerId", "LastProgressUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingTimeSettings_ContentTypeId",
                table: "ReadingTimeSettings",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingTimeSettings_UserId_ContentTypeId",
                table: "ReadingTimeSettings",
                columns: new[] { "UserId", "ContentTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingTimeSettings");

            migrationBuilder.DropIndex(
                name: "IX_Books_OwnerId_LastProgressUpdatedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "LastProgressUpdatedAt",
                table: "Books");
        }
    }
}
