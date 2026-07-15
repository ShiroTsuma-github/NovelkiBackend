using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" AS u
                SET "CreatedAt" = COALESCE(
                    (
                        SELECT MIN(source."Created")
                        FROM (
                            SELECT MIN("Created") AS "Created" FROM "Books" WHERE "OwnerId" = u."Id"
                            UNION ALL
                            SELECT MIN("Created") AS "Created" FROM "Tags" WHERE "OwnerId" = u."Id"
                            UNION ALL
                            SELECT MIN("Created") AS "Created" FROM "RefreshTokens" WHERE "UserId" = u."Id"
                        ) AS source
                        WHERE source."Created" IS NOT NULL
                    ),
                    NOW()
                );
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");
        }
    }
}
