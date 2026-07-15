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
            migrationBuilder.Sql("""
                ALTER TABLE "AspNetUsers"
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone;

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
                )
                WHERE u."CreatedAt" IS NULL;

                ALTER TABLE "AspNetUsers"
                ALTER COLUMN "CreatedAt" SET DEFAULT NOW();

                ALTER TABLE "AspNetUsers"
                ALTER COLUMN "CreatedAt" SET NOT NULL;
                """);
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
