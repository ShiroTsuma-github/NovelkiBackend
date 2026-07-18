using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicBookSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookShareAuthorPromotions",
                columns: table => new
                {
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookShareAuthorPromotions", x => x.AuthorId);
                    table.ForeignKey(
                        name: "FK_BookShareAuthorPromotions_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookShareTagPromotions",
                columns: table => new
                {
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookShareTagPromotions", x => x.TagId);
                    table.ForeignKey(
                        name: "FK_BookShareTagPromotions_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicBookSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NormalizedPrimaryTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AlternativeTitlesJson = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AuthorOtherNamesJson = table.Column<string>(type: "text", nullable: false),
                    PublicAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GenresJson = table.Column<string>(type: "text", nullable: false),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    PublicTagIdsJson = table.Column<string>(type: "text", nullable: false),
                    CoverStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CoverThumbnailStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CoverMimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SnapshotAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicBookSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicBookSnapshots_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicBookSnapshots_Books_SourceBookId",
                        column: x => x.SourceBookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookSnapshots_NormalizedPrimaryTitle",
                table: "PublicBookSnapshots",
                column: "NormalizedPrimaryTitle");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookSnapshots_OwnerId_SnapshotAt",
                table: "PublicBookSnapshots",
                columns: new[] { "OwnerId", "SnapshotAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookSnapshots_SourceBookId",
                table: "PublicBookSnapshots",
                column: "SourceBookId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookShareAuthorPromotions");

            migrationBuilder.DropTable(
                name: "BookShareTagPromotions");

            migrationBuilder.DropTable(
                name: "PublicBookSnapshots");
        }
    }
}
